using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using single_logout_app_a.Models;
using System.Text.Json;
using System;
using System.Globalization;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "oidc";
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(120);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async ctx =>
            {
                var refreshToken = ctx.Properties.GetTokenValue("refresh_token");
                // Retrieve current expiry from access token
                DateTimeOffset.TryParse(ctx.Properties.GetTokenValue("expires_at"), out DateTimeOffset expiresAt);
                var timeToExpiry = expiresAt.Subtract(DateTimeOffset.Now);

                // Check if time is close to expiry using threshold. You can change this to prevent long wait time during development
                var refreshThreshold = TimeSpan.FromMinutes(59);
                if(!string.IsNullOrWhiteSpace(refreshToken) && timeToExpiry < refreshThreshold)
                {
                    // Refresh Token
                    using (var refreshClient = new HttpClient()) 
                    {
                        var issuerUri = new Uri(builder.Configuration["Okta:Issuer"]);
                        refreshClient.BaseAddress = new Uri($"{issuerUri.Scheme}://{issuerUri.Host}");

                        var request = new HttpRequestMessage(HttpMethod.Post, $"{issuerUri.AbsolutePath}/v1/token");
                        request.Headers.Authorization = new AuthenticationHeaderValue(
                            "Basic", Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes($"{builder.Configuration["Okta:ClientId"]}:{builder.Configuration["Okta:ClientSecret"]}"))
                            );

                        var requestBody = new FormUrlEncodedContent(
                            new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                                // TODO: Pull the host dynamically
                                new KeyValuePair<string, string>("redirect_uri", "https://localhost:7005/signout-callback-oidc"),
                                new KeyValuePair<string, string>("scope", "openid profile email offline_access"),
                                new KeyValuePair<string, string>("refresh_token", refreshToken)
                            });
                        request.Content = requestBody;

                        using (var response = await refreshClient.SendAsync(request).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                                var tokenResponse = JsonConvert.DeserializeObject<RefreshTokenResponse>(responseStr);

                                var expirationValue = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o", CultureInfo.InvariantCulture);
                                ctx.Properties.StoreTokens(new[]
                                {
                                    new AuthenticationToken { Name = "refresh_token", Value = tokenResponse.RefreshToken },
                                    new AuthenticationToken { Name = "access_token", Value = tokenResponse.AccessToken },
                                    new AuthenticationToken { Name = "id_token", Value = tokenResponse.IdToken },
                                    new AuthenticationToken { Name = "expires_at", Value = expirationValue }
                                });
                                ctx.ShouldRenew = true;
                            }
                            else
                            {
                                ctx.RejectPrincipal();
                                await ctx.HttpContext.SignOutAsync();
                            }
                        }
                    }
                }
            }
        };
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authority = builder.Configuration["Okta:Issuer"];
        options.ClientId = builder.Configuration["Okta:ClientId"];
        options.ClientSecret = builder.Configuration["Okta:ClientSecret"];
        options.ResponseType = "code";
        options.ResponseMode = "query";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.UseTokenLifetime = false;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("offline_access");
    });
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();



app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

