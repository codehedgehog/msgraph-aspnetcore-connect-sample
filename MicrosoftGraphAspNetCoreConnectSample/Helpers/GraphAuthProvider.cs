/*
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license.
*  See LICENSE in the source repository root for complete license information.
*/


namespace MicrosoftGraphAspNetCoreConnectSample.Helpers
{
	using Microsoft.Extensions.Configuration;
	using Microsoft.Graph;
	using Microsoft.Identity.Client;
	using MicrosoftGraphAspNetCoreConnectSample.Extensions;
	using System;
	using System.Security.Cryptography.X509Certificates;
	using System.Threading.Tasks;
	using System.Collections.Generic;
	using System.Net.Http;


	public class GraphAuthProvider : IGraphAuthProvider
	{
		private IConfidentialClientApplication _app;
		private readonly string[] _scopes;

		public GraphAuthProvider(IConfiguration configuration)
		{
			var azureOptions = new AzureAdOptions();
			configuration.Bind("AzureAd", azureOptions);



			var certificateThumbprint = azureOptions.CertificateThumbprint;
			// defaulting to CurrentUser certificate store under My (Personal), change these if stored elsewhere
			X509Certificate2 cert = GetCertificate(certificateThumbprint, StoreName.My, StoreLocation.CurrentUser);

			// More info about MSAL Client Applications: https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/Client-Applications
			_app = ConfidentialClientApplicationBuilder.Create(azureOptions.ClientId)
				//.WithClientSecret(azureOptions.ClientSecret)
				//.WithAuthority(azureCloudInstance: AzureCloudInstance.AzurePublic, authorityAudience: AadAuthorityAudience.AzureAdMyOrg)
				.WithAuthority(azureCloudInstance: AzureCloudInstance.AzurePublic, tenantId: new Guid(azureOptions.TenantId))
				.WithRedirectUri(azureOptions.BaseUrl + azureOptions.CallbackPath)
				.WithCertificate(cert)
				.Build();
			Authority = _app.Authority;
			_scopes = azureOptions.GraphScopes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		}

		public string Authority { get; }

		// Gets an access token. First tries to get the access token from the token cache.
		// Using password (secret) to authenticate. Production apps should use a certificate.
		public async Task<string> GetUserAccessTokenAsync(string userId)
		{
			var account = await _app.GetAccountAsync(userId);
			if (account == null) throw new ServiceException(new Error
			{
				Code = "TokenNotFound",
				Message = "User not found in token cache. Maybe the server was restarted."
			});
			try
			{
				var result = await _app.AcquireTokenSilent(_scopes, account).ExecuteAsync();
				return result.AccessToken;
			}
			// Unable to retrieve the access token silently.
			catch (Exception)
			{
				throw new ServiceException(new Error
				{
					Code = GraphErrorCode.AuthenticationFailure.ToString(),
					Message = "Caller needs to authenticate. Unable to retrieve the access token silently."
				});
			}
		}

		public async Task<AuthenticationResult> GetUserAccessTokenByAuthorizationCode(string authorizationCode)
		{
			return await _app.AcquireTokenByAuthorizationCode(_scopes, authorizationCode).ExecuteAsync();
		}

		private static X509Certificate2 GetCertificate(string thumbprint, StoreName storeName, StoreLocation storeLocation)
		{
			X509Store store = new X509Store(storeName, storeLocation);
			try
			{
				store.Open(OpenFlags.ReadOnly);

				var col = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
				if (col == null || col.Count == 0)
				{
					return null;
				}
				return col[0];
			}
			finally
			{
				store.Close();
			}
		}


	}

	public interface IGraphAuthProvider
	{
		string Authority { get; }

		Task<string> GetUserAccessTokenAsync(string userId);

		Task<AuthenticationResult> GetUserAccessTokenByAuthorizationCode(string authorizationCode);
	}
}