using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Claims;
using System.Text.Json;

namespace OAuth;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();

		services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
			options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
			options.DefaultChallengeScheme = "discord";
		})
		.AddCookie()
		.AddOAuth("discord", options =>
		{
			// OAuth provider configuration
			options.ClientId = "";
			options.ClientSecret = "";
			options.CallbackPath = new PathString("/auth/callback/discord");

			// OAuth provider endpoints
			options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
			options.TokenEndpoint = "https://discord.com/api/oauth2/token";
			options.UserInformationEndpoint = "https://discord.com/api/users/@me";
			options.Scope.Add("identify");
			options.Scope.Add("email");
			options.SaveTokens = true;
			options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
			options.ClaimActions.MapJsonKey("sub", "id");
			options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
			
			// OAuth events
			options.Events = new OAuthEvents
			{
				OnCreatingTicket = async context =>
				{
					using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
					request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
					using var result = await context.Backchannel.SendAsync(request);
					var user = await result.Content.ReadFromJsonAsync<JsonElement>();
					context.RunClaimActions(user);
				},
				OnRemoteFailure = context =>
				{
					// Handle errors during OAuth authentication
					return Task.CompletedTask;
				}
			};
		});
	}

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

		app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("Welcome to running ASP.NET Core on AWS Lambda");
            });
			endpoints.MapGet("/login", () =>
			{
				return Results.Challenge(new AuthenticationProperties()
				{
					RedirectUri = "https://localhost:56238"
				}, authenticationSchemes: ["discord"]);
			});
			endpoints.MapGet("/secure", (ClaimsPrincipal principal) => $"Hello {principal?.Identity?.Name}").RequireAuthorization();
		});
    }
}