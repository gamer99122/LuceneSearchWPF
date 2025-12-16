using Microsoft.Extensions.DependencyInjection;
﻿using System.Windows;
﻿using System.Text;
﻿using System; // Added for IServiceProvider
﻿using LuceneSearchWPFApp.Services;
﻿using LuceneSearchWPFApp.Services.Interfaces;
﻿using LuceneSearchWPFApp.ViewModels;
﻿
﻿namespace LuceneSearchWPFApp;
﻿
﻿/// <summary>
﻿/// Interaction logic for App.xaml
﻿/// </summary>
﻿public partial class App : Application
﻿{
﻿    public static IServiceProvider ServiceProvider { get; private set; }
﻿
﻿    protected override void OnStartup(StartupEventArgs e)
﻿    {
﻿        base.OnStartup(e);
﻿        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
﻿        ConfigureServices();
﻿    }
﻿
﻿    private void ConfigureServices()
﻿    {
﻿        var serviceCollection = new ServiceCollection();
﻿
﻿        // Register services
﻿        serviceCollection.AddSingleton<IConfigurationService, ConfigurationService>();
﻿        serviceCollection.AddSingleton<ISearchService, SearchService>();
﻿        serviceCollection.AddSingleton<IIndexService, IndexService>();
﻿
﻿        // Register ViewModels
﻿        serviceCollection.AddTransient<MainViewModel>();
﻿
﻿        ServiceProvider = serviceCollection.BuildServiceProvider();
﻿    }
﻿}
﻿

