// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/tools/BooleanVisibilityConverter.cs
// 📌 Amac: Paylasilan kartlarin gorunurluk bindingini WinUI Visibility tipine donusturur
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Kullanici secimlerine gore View gorunurlugu kontrolu; is mantigi tasimaz
//
// Bagimli Oldugu Katman: Tool | View

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace TurkuazInstaller.WinUI.Tools;

public sealed class BooleanVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool enabled && enabled
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException("Promotion visibility is read-only.");
    }
}
