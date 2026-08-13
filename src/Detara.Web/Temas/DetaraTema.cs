using MudBlazor;

namespace Detara.Web.Temas;

public static class DetaraTema
{
    public static MudTheme Valor { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = DetaraTokens.PrimaryDark,
            Secondary = DetaraTokens.Secondary,
            Tertiary = DetaraTokens.Teal,
            Background = "#F8FAFC",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#111827",
            DrawerBackground = "#0B1220",
            DrawerText = "#CBD5E1",
            TextPrimary = "#111827",
            TextSecondary = "#64748B",
            Divider = "#E2E8F0",
            LinesDefault = "#CBD5E1",
            Success = DetaraTokens.Success,
            Warning = DetaraTokens.Warning,
            Error = DetaraTokens.Error,
            Info = DetaraTokens.Info
        },
        PaletteDark = new PaletteDark
        {
            Primary = DetaraTokens.Primary,
            Secondary = "#60A5FA",
            Tertiary = DetaraTokens.PrimaryLight,
            Background = "#0B1220",
            Surface = "#111827",
            AppbarBackground = "#111827",
            AppbarText = "#F8FAFC",
            DrawerBackground = "#07111D",
            DrawerText = "#CBD5E1",
            TextPrimary = "#F8FAFC",
            TextSecondary = "#94A3B8",
            Divider = "#253247",
            LinesDefault = "#334155",
            Success = DetaraTokens.Success,
            Warning = DetaraTokens.Warning,
            Error = DetaraTokens.Error,
            Info = DetaraTokens.Info
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "system-ui", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "sans-serif"],
                FontSize = "1rem",
                LineHeight = "1.5"
            },
            H1 = new H1Typography { FontSize = "2rem", FontWeight = "800", LineHeight = "2.5rem" },
            H2 = new H2Typography { FontSize = "1.5rem", FontWeight = "700", LineHeight = "2rem" },
            H3 = new H3Typography { FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.75rem" },
            H4 = new H4Typography { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.625rem" },
            Body1 = new Body1Typography { FontSize = "1rem", FontWeight = "400", LineHeight = "1.5rem" },
            Body2 = new Body2Typography { FontSize = ".875rem", FontWeight = "400", LineHeight = "1.25rem" },
            Caption = new CaptionTypography { FontSize = ".75rem", FontWeight = "500", LineHeight = "1rem" }
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "248px",
            DrawerMiniWidthLeft = "72px"
        }
    };
}
