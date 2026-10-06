using System.Windows.Markup;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Localization;

/// <summary>XAML access to translations: <c>Text="{l:Tr sidebar.scan_folder}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
