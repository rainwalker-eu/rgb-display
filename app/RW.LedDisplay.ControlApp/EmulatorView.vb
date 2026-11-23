Imports Avalonia
Imports Avalonia.Controls
Imports Avalonia.Media

' The 32x10 panel as a grid of squares on a 10 px pitch
Public Class EmulatorView
    Inherits Control

    Private Const Pitch As Integer = 10
    Private screen As System.Drawing.Color(,)

    Public Sub New()
        Width = DisplayEmulator.Width * Pitch
        Height = DisplayEmulator.Height * Pitch
    End Sub

    ' Keeps the array it is given, so the caller must not reuse it
    Public Sub Show(frame As System.Drawing.Color(,))
        screen = frame
        InvalidateVisual()
    End Sub

    Public Overrides Sub Render(context As DrawingContext)
        context.FillRectangle(Brushes.Black, New Rect(Bounds.Size))
        If screen Is Nothing Then Return
        For x = 0 To DisplayEmulator.Width - 1
            For y = 0 To DisplayEmulator.Height - 1
                Dim c = screen(x, y)
                If c.R = 0 AndAlso c.G = 0 AndAlso c.B = 0 Then Continue For
                context.FillRectangle(New SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)),
                                      New Rect(x * Pitch + 1, y * Pitch + 1, Pitch - 2, Pitch - 2))
            Next
        Next
    End Sub
End Class
