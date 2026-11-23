Imports Avalonia
Imports Avalonia.Controls.ApplicationLifetimes
Imports Avalonia.Themes.Fluent

' Cross-platform control application for the rgb3210 display (Linux and Windows). The interface is
' built in code: Avalonia compiles XAML code-behind for C# and F# only.
Module Program
    <STAThread>
    Sub Main(args As String())
        AppBuilder.Configure(Of App)().UsePlatformDetect().StartWithClassicDesktopLifetime(args)
    End Sub
End Module

Public Class App
    Inherits Application

    Public Overrides Sub Initialize()
        Styles.Add(New FluentTheme())
    End Sub

    Public Overrides Sub OnFrameworkInitializationCompleted()
        Dim desktop = TryCast(ApplicationLifetime, IClassicDesktopStyleApplicationLifetime)
        If desktop IsNot Nothing Then desktop.MainWindow = New MainWindow()
        MyBase.OnFrameworkInitializationCompleted()
    End Sub
End Class
