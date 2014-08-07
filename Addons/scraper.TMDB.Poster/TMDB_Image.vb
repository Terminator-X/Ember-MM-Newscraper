﻿' ################################################################################
' #                             EMBER MEDIA MANAGER                              #
' ################################################################################
' ################################################################################
' # This file is part of Ember Media Manager.                                    #
' #                                                                              #
' # Ember Media Manager is free software: you can redistribute it and/or modify  #
' # it under the terms of the GNU General Public License as published by         #
' # the Free Software Foundation, either version 3 of the License, or            #
' # (at your option) any later version.                                          #
' #                                                                              #
' # Ember Media Manager is distributed in the hope that it will be useful,       #
' # but WITHOUT ANY WARRANTY; without even the implied warranty of               #
' # MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the                #
' # GNU General Public License for more details.                                 #
' #                                                                              #
' # You should have received a copy of the GNU General Public License            #
' # along with Ember Media Manager.  If not, see <http://www.gnu.org/licenses/>. #
' ###############################################################################

Imports System.IO
Imports EmberAPI
Imports WatTmdb
Imports ScraperModule.TMDBg
Imports NLog
Imports System.Diagnostics

Public Class TMDB_Image
    Implements Interfaces.ScraperModule_Image_Movie


#Region "Fields"
    Shared logger As Logger = NLog.LogManager.GetCurrentClassLogger()
    Public Shared ConfigOptions As New Structures.MovieScrapeOptions
    Public Shared ConfigScrapeModifier As New Structures.ScrapeModifier
    Public Shared _AssemblyName As String

    Private TMDBId As String
    Private _TMDBg As TMDBg.Scraper
    Private TMDB As TMDB.Scraper

    ''' <summary>
    ''' Scraping Here
    ''' </summary>
    ''' <remarks></remarks>
    Private strPrivateAPIKey As String = String.Empty
    Private _MySettings As New sMySettings
    Private _Name As String = "TMDB_Image"
    Private _ScraperEnabled As Boolean = False
    Private _setup As frmTMDBMediaSettingsHolder
    Private _TMDBConf As V3.TmdbConfiguration
    Private _TMDBConfE As V3.TmdbConfiguration
    Private _TMDBApi As V3.Tmdb 'preferred language
    Private _TMDBApiE As V3.Tmdb 'english language
    Private _TMDBApiA As V3.Tmdb 'all languages

#End Region 'Fields

#Region "Events"

    Public Event ModuleSettingsChanged() Implements Interfaces.ScraperModule_Image_Movie.ModuleSettingsChanged

    Public Event MovieScraperEvent(ByVal eType As Enums.ScraperEventType_Movie, ByVal Parameter As Object) Implements Interfaces.ScraperModule_Image_Movie.MovieScraperEvent

    Public Event SetupScraperChanged(ByVal name As String, ByVal State As Boolean, ByVal difforder As Integer) Implements Interfaces.ScraperModule_Image_Movie.ScraperSetupChanged

    Public Event SetupNeedsRestart() Implements Interfaces.ScraperModule_Image_Movie.SetupNeedsRestart

    Public Event PostersDownloaded(ByVal Posters As List(Of MediaContainers.Image)) Implements Interfaces.ScraperModule_Image_Movie.PostersDownloaded

    Public Event ProgressUpdated(ByVal iPercent As Integer) Implements Interfaces.ScraperModule_Image_Movie.ProgressUpdated

#End Region 'Events

#Region "Properties"

    ReadOnly Property ModuleName() As String Implements Interfaces.ScraperModule_Image_Movie.ModuleName
        Get
            Return _Name
        End Get
    End Property

    ReadOnly Property ModuleVersion() As String Implements Interfaces.ScraperModule_Image_Movie.ModuleVersion
        Get
            Return System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly.Location).FileVersion.ToString
        End Get
    End Property

    Property ScraperEnabled() As Boolean Implements Interfaces.ScraperModule_Image_Movie.ScraperEnabled
        Get
            Return _ScraperEnabled
        End Get
        Set(ByVal value As Boolean)
            _ScraperEnabled = value
        End Set
    End Property

#End Region 'Properties

#Region "Methods"
    Function QueryScraperCapabilities(ByVal cap As Enums.ScraperCapabilities) As Boolean Implements Interfaces.ScraperModule_Image_Movie.QueryScraperCapabilities
        Select Case cap
            Case Enums.ScraperCapabilities.Fanart
                Return ConfigScrapeModifier.Fanart
            Case Enums.ScraperCapabilities.Poster
                Return ConfigScrapeModifier.Poster
        End Select
        Return False
    End Function

    Private Sub Handle_ModuleSettingsChanged()
        RaiseEvent ModuleSettingsChanged()
    End Sub

    Private Sub Handle_SetupNeedsRestart()
        RaiseEvent SetupNeedsRestart()
    End Sub

    Private Sub Handle_SetupScraperChanged(ByVal state As Boolean, ByVal difforder As Integer)
        ScraperEnabled = state
        RaiseEvent SetupScraperChanged(String.Concat(Me._Name, "Scraper"), state, difforder)
    End Sub

    Sub Init(ByVal sAssemblyName As String) Implements Interfaces.ScraperModule_Image_Movie.Init
        _AssemblyName = sAssemblyName
        LoadSettings()
        'Must be after Load settings to retrieve the correct API key
        _TMDBApi = New WatTmdb.V3.Tmdb(_MySettings.TMDBAPIKey, _MySettings.TMDBLanguage)
        If IsNothing(_TMDBApi) Then
            logger.Error(Master.eLang.GetString(938, "TheMovieDB API is missing or not valid"), _TMDBApi.Error.status_message)
        Else
            If Not IsNothing(_TMDBApi.Error) AndAlso _TMDBApi.Error.status_message.Length > 0 Then
                logger.Error(_TMDBApi.Error.status_message, _TMDBApi.Error.status_code.ToString())
            End If
        End If
        _TMDBConf = _TMDBApi.GetConfiguration()
        _TMDBApiE = New WatTmdb.V3.Tmdb(_MySettings.TMDBAPIKey)
        _TMDBConfE = _TMDBApiE.GetConfiguration()
        _TMDBApiA = New WatTmdb.V3.Tmdb(_MySettings.TMDBAPIKey, "")
        _TMDBg = New TMDBg.Scraper(_TMDBConf, _TMDBConfE, _TMDBApi, _TMDBApiE, _TMDBApiA, True)
        TMDB = New TMDB.Scraper(_TMDBConf, _TMDBConfE, _TMDBApi, _TMDBApiE, _TMDBApiA, _MySettings)
    End Sub

    Function InjectSetupScraper() As Containers.SettingsPanel Implements Interfaces.ScraperModule_Image_Movie.InjectSetupScraper
        Dim Spanel As New Containers.SettingsPanel
        _setup = New frmTMDBMediaSettingsHolder
        LoadSettings()
        _setup.cbEnabled.Checked = _ScraperEnabled
        _setup.chkScrapePoster.Checked = ConfigScrapeModifier.Poster
        _setup.chkScrapeFanart.Checked = ConfigScrapeModifier.Fanart
        _setup.txtTMDBApiKey.Text = strPrivateAPIKey
        _setup.cbTMDBLanguage.Text = _MySettings.TMDBLanguage
        _setup.chkFallBackEng.Checked = _MySettings.FallBackEng
        _setup.chkTMDBLanguagePrefOnly.Checked = _MySettings.TMDBLanguagePrefOnly
        _setup.Lang = _setup.cbTMDBLanguage.Text
        _setup.API = _setup.txtTMDBApiKey.Text

        _setup.orderChanged()

        Spanel.Name = String.Concat(Me._Name, "Scraper")
        Spanel.Text = Master.eLang.GetString(937, "TMDB")
        Spanel.Prefix = "TMDBMovieMedia_"
        Spanel.Order = 110
        Spanel.Parent = "pnlMovieMedia"
        Spanel.Type = Master.eLang.GetString(36, "Movies")
        Spanel.ImageIndex = If(Me._ScraperEnabled, 9, 10)
        Spanel.Panel = Me._setup.pnlSettings

        AddHandler _setup.SetupScraperChanged, AddressOf Handle_SetupScraperChanged
        AddHandler _setup.ModuleSettingsChanged, AddressOf Handle_ModuleSettingsChanged
        AddHandler _setup.SetupNeedsRestart, AddressOf Handle_SetupNeedsRestart
        Return Spanel
    End Function

    Sub LoadSettings()

        strPrivateAPIKey = clsAdvancedSettings.GetSetting("TMDBAPIKey", "")
        _MySettings.TMDBAPIKey = If(String.IsNullOrEmpty(strPrivateAPIKey), "44810eefccd9cb1fa1d57e7b0d67b08d", strPrivateAPIKey)
        _MySettings.FallBackEng = clsAdvancedSettings.GetBooleanSetting("FallBackEn", False)
        _MySettings.TMDBLanguage = clsAdvancedSettings.GetSetting("TMDBLanguage", "en")
        _MySettings.TMDBLanguagePrefOnly = clsAdvancedSettings.GetBooleanSetting("TMDBLanguagePrefOnly", True)

        ConfigScrapeModifier.Poster = clsAdvancedSettings.GetBooleanSetting("DoPoster", True)
        ConfigScrapeModifier.Fanart = clsAdvancedSettings.GetBooleanSetting("DoFanart", True)

    End Sub

    Function Scraper(ByRef DBMovie As Structures.DBMovie, ByVal Type As Enums.ScraperCapabilities, ByRef ImageList As List(Of MediaContainers.Image)) As Interfaces.ModuleResult Implements Interfaces.ScraperModule_Image_Movie.Scraper

        LoadSettings()

        If String.IsNullOrEmpty(DBMovie.Movie.TMDBID) Then
            _TMDBg.GetMovieID(DBMovie)
        End If

        ImageList = TMDB.GetTMDBImages(DBMovie.Movie.TMDBID, Type)

        Return New Interfaces.ModuleResult With {.breakChain = False}
    End Function

    Function Scraper(ByRef DBMovieSet As Structures.DBMovieSet, ByVal Type As Enums.ScraperCapabilities, ByRef ImageList As List(Of MediaContainers.Image)) As Interfaces.ModuleResult Implements Interfaces.ScraperModule_Image_Movie.Scraper

        LoadSettings()

        ImageList = TMDB.GetTMDBCollectionImages(DBMovieSet.MovieSet.ID, Type)

        Return New Interfaces.ModuleResult With {.breakChain = False}
    End Function

    Sub SaveSettings()
        Using settings = New clsAdvancedSettings()
            settings.SetBooleanSetting("DoPoster", ConfigScrapeModifier.Poster)
            settings.SetBooleanSetting("DoFanart", ConfigScrapeModifier.Fanart)

            settings.SetSetting("TMDBAPIKey", _setup.txtTMDBApiKey.Text)
            settings.SetBooleanSetting("FallBackEn", _MySettings.FallBackEng)
            settings.SetBooleanSetting("TMDBLanguagePrefOnly", _MySettings.TMDBLanguagePrefOnly)
            settings.SetSetting("TMDBLanguage", _MySettings.TMDBLanguage)
        End Using
    End Sub

    Sub SaveSetupScraper(ByVal DoDispose As Boolean) Implements Interfaces.ScraperModule_Image_Movie.SaveSetupScraper
        _MySettings.TMDBLanguage = _setup.cbTMDBLanguage.Text
        _MySettings.FallBackEng = _setup.chkFallBackEng.Checked
        _MySettings.TMDBLanguagePrefOnly = _setup.chkTMDBLanguagePrefOnly.Checked
        ConfigScrapeModifier.Poster = _setup.chkScrapePoster.Checked
        ConfigScrapeModifier.Fanart = _setup.chkScrapeFanart.Checked
        SaveSettings()
        'ModulesManager.Instance.SaveSettings()
        If DoDispose Then
            RemoveHandler _setup.SetupScraperChanged, AddressOf Handle_SetupScraperChanged
            RemoveHandler _setup.ModuleSettingsChanged, AddressOf Handle_ModuleSettingsChanged
            RemoveHandler _setup.SetupNeedsRestart, AddressOf Handle_SetupNeedsRestart
            _setup.Dispose()
        End If
    End Sub

    Public Sub ScraperOrderChanged() Implements EmberAPI.Interfaces.ScraperModule_Image_Movie.ScraperOrderChanged
        _setup.orderChanged()
    End Sub

#End Region 'Methods

#Region "Nested Types"

    Structure sMySettings

#Region "Fields"
        Dim TMDBAPIKey As String
        Dim TMDBLanguage As String
        Dim TMDBLanguagePrefOnly As Boolean
        Dim FallBackEng As Boolean
#End Region 'Fields

    End Structure

#End Region 'Nested Types

End Class