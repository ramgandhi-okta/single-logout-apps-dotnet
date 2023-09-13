# Okta SLO Dotnet

This project can be used to test SLO EA feature in okta


## Prerequisites
- Okta Tenant
- Enable **Front-channel Single Logout** feature in Settings > Features

## Project Setup
- Create two OIDC web applications and setup SLO configuration - [Reference](https://developer.okta.com/docs/guides/single-logout/main/)
- In Okta admin console update the following fields,
    - App A
        - Sign-in redirect URIs = https://localhost:7005/signin-oidc
        - Logout redirect URIs = https://localhost:7005/signout-callback-oidc
        - Logout request URL = https://localhost:7005/signout-oidc
    - App B
        - Sign-in redirect URIs = https://localhost:7127/signin-oidc
        - Logout redirect URIs = https://localhost:7127/signout-callback-oidc
        - Logout request URL = https://localhost:7127/signout-oidc
- Set up startup projects to run both projects at the same time
- Add `Issuer`, `ClientId` and `ClientSecret` under Okta section in **appsettings.json** file in both projects
     
## Considerations
- This seems to work only on same browser and opening apps in differnt tabs (Tested in Chrome and Safari)
