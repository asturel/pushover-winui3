This branch contains a refactor to use the .NET Generic Host and Microsoft.Extensions.DependencyInjection.

Changes summary:
- Add hosting, http factory and logging package references
- Initialize an IHost in App.xaml.cs and register services
- Make ConfigService use IOptionsMonitor<AppConfig>
- Inject IMessageStorage and IServiceProvider into MainWindow
- Refactor PushoverWebSocketService to use IHttpClientFactory and ILogger and accept device credentials in StartAsync

This file is a small marker commit to trigger CI for branch feature/di-host-refactor.
