using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Chishazi;
using Chishazi.Options;
using Chishazi.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient());
builder.Services.AddSingleton(new GoogleSheetsOptions
{
    ClientId = builder.Configuration["GoogleSheets:ClientId"] ?? string.Empty,
    SpreadsheetId = builder.Configuration["GoogleSheets:SpreadsheetId"] ?? string.Empty
});
builder.Services.AddScoped<GoogleAuthorizationService>();
builder.Services.AddScoped<GoogleSheetsClient>();
builder.Services.AddScoped<BrowserCacheService>();
builder.Services.AddScoped<SpreadsheetStore>();
builder.Services.AddSingleton<TagSheetParser>();
builder.Services.AddSingleton<RecipeSheetParser>();
builder.Services.AddSingleton<SpreadsheetDiffService>();
builder.Services.AddSingleton<SpreadsheetMutationService>();

await builder.Build().RunAsync();
