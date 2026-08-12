using MudBlazor;

namespace Detara.Web.Temas;

public static class DetaraTema
{
    public static MudTheme Valor { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#146C5A",
            Secondary = "#526965",
            Background = "#F5F7F6",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#24312F",
            DrawerBackground = "#102A26",
            DrawerText = "#D7E3E0",
            Error = "#B42318",
            Success = "#16794E"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Inter", "sans-serif"] }
        },
        LayoutProperties = new LayoutProperties { DrawerWidthLeft = "252px" }
    };
}
