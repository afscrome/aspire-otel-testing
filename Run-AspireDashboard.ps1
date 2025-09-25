docker run -it `
    -p 18888:18888 `
    -p 14317:18889 `
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
    --name aspire-dashboard `
    mcr.microsoft.com/dotnet/nightly/aspire-dashboard