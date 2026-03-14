namespace DotPilot.UITests.Harness;

public class Constants
{
    public static readonly string WebAssemblyDefaultUri = BrowserTestEnvironment.WebAssemblyUri;
    public static readonly string iOSAppName = "com.companyname.DotPilot";
    public static readonly string AndroidAppName = "com.companyname.DotPilot";
    public static readonly string iOSDeviceNameOrId = "iPad Pro (12.9-inch) (3rd generation)";

    public static readonly Platform CurrentPlatform = Platform.Browser;
    public static readonly Browser WebAssemblyBrowser = Browser.Chrome;
}
