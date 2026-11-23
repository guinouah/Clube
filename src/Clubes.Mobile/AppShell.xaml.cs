namespace Clubes.Mobile;

public partial class AppShell : Shell
{
	public AppShell(MainPage mainPage, EventsPage eventsPage, DuesPage duesPage, MorePage morePage, LoginPage loginPage)
	{
		InitializeComponent();

		var tabBar = new TabBar();
		tabBar.Items.Add(CreateTab("Início", "inicio", mainPage));
		tabBar.Items.Add(CreateTab("Eventos", "eventos", eventsPage));
		tabBar.Items.Add(CreateTab("Mensalidades", "mensalidades", duesPage));
		tabBar.Items.Add(CreateTab("Mais", "mais", morePage));
		tabBar.Items.Add(CreateTab("Conta", "conta", loginPage));
		Items.Add(tabBar);
		CurrentItem = tabBar.Items[tabBar.Items.Count - 1];
	}

	private static Tab CreateTab(string title, string route, Page page)
	{
		var tab = new Tab { Title = title, Route = route };
		tab.Items.Add(new ShellContent { Title = title, Route = $"{route}-pagina", Content = page });
		return tab;
	}
}
