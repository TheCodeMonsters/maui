using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AndroidX.Navigation;
using AndroidX.Navigation.Fragment;
using AndroidX.Navigation.UI;

namespace Microsoft.Maui
{

	// TODO MAUI MAKE PRIVATE or make it proxy and probably rename
	public class NavGraphDestination : NavGraph
	{
		public IReadOnlyList<IView> NavigationStack { get; private set; } = new List<IView>();
		public Dictionary<IView, int> Pages = new Dictionary<IView, int>();

		public NavGraphDestination(Navigator navGraphNavigator) : base(navGraphNavigator)
		{
			Id = global::Android.Views.View.GenerateViewId();
		}

		internal bool? IsPopping { get; private set; }
		internal bool IsAnimated { get; private set; } = true;

		// all of this weirdness is because AFAICT you can't remove things from the navigation stack
		public void ReShuffleDestinations(
			IReadOnlyList<IView> pages,
			bool animated,
			NavigationLayout navigationLayout)
		{
			var navController = navigationLayout.NavHost.NavController;

			// TODO this needs a bit more behavior because what about swapping out stuff
			if (pages[pages.Count - 1] == NavigationStack[NavigationStack.Count - 1])
				IsPopping = null;
			else
				IsPopping = pages.Count < NavigationStack.Count;

			IsAnimated = animated;

			// this means the currently visible page hasn't changed so don't do anything
			// TODO MAUI test remove page on root
			// If they've removed all the pages except one then we process the navigation
			// to update the app bar
			if (pages[pages.Count - 1] == NavigationStack[NavigationStack.Count - 1] &&
				pages.Count > 1 &&
				NavigationStack.Count > 1)
			{
				NavigationStack = new List<IView>(pages);
				return;
			}

			var iterator = navigationLayout.NavHost.NavController.BackStack.Iterator();
			var fragmentNavDestinations = new List<FragmentNavDestination>();
			var bsEntry = new List<NavBackStackEntry>();

			while (iterator.HasNext)
			{
				if (iterator.Next() is NavBackStackEntry nbse &&
					nbse.Destination is FragmentNavDestination nvd)
				{
					fragmentNavDestinations.Add(nvd);
					bsEntry.Add(nbse);
				}
			}

			Pages.Clear();
			if (fragmentNavDestinations.Count < pages.Count)
			{
				for (int i = 0; i < pages.Count; i++)
				{
					// TODO cleanup into method
					if (fragmentNavDestinations.Count > i)
					{
						Pages.Add(pages[i], fragmentNavDestinations[i].Id);
						fragmentNavDestinations[i].Page = pages[i];
					}
					else
					{
						// AddDestination adds Pages Ids
						var dest = AddDestination(pages[i], navigationLayout);
						// TODO VALIDATE plopping lots of pages here
						navController.Navigate(dest.Id);
					}
				}
			}
			else if (pages.Count == fragmentNavDestinations.Count)
			{
				int lastFragId = fragmentNavDestinations[pages.Count - 1].Id;

				for (int i = 0; i < pages.Count; i++)
				{
					Pages.Add(pages[i], fragmentNavDestinations[i].Id);

					if (fragmentNavDestinations[i].Page != pages[i])
						fragmentNavDestinations[i].Page = pages[i];
				}

				navController.PopBackStack();
				navController.Navigate(lastFragId);
			}
			// user is popping to root
			else if (pages.Count == 1)
			{
				// TODO MAUI work with cleaning up fragments before actually firing navigation
				Pages.Add(pages[0], fragmentNavDestinations[0].Id);
				fragmentNavDestinations[0].Page = pages[0];
				navController.PopBackStack(fragmentNavDestinations[0].Id, false);
			}
			else
			{
				int popToId = fragmentNavDestinations[pages.Count - 1].Id;
				for (int i = 0; i < pages.Count; i++)
				{
					Pages.Add(pages[i], fragmentNavDestinations[i].Id);

					if (fragmentNavDestinations[i].Page != pages[i])
						fragmentNavDestinations[i].Page = pages[i];
				}

				navController.PopBackStack(popToId, false);
			}


			foreach (var thing in fragmentNavDestinations)
			{
				if (!Pages.Values.ToList().Contains(thing.Id))
				{
					this.Remove(thing);
				}
			}

			NavigationStack = new List<IView>(pages);
			navigationLayout.ToolbarReady();
		}

		public FragmentNavDestination AddDestination(
			IView page,
			NavigationLayout navigationLayout)
		{
			var destination = new FragmentNavDestination(page, navigationLayout, this);
			AddDestination(destination);
			return destination;
		}

		internal List<int> ApplyPagesToGraph(
			IReadOnlyList<IView> pages,
			NavigationLayout navigationLayout)
		{
			var navController = navigationLayout.NavHost.NavController;

			// We are subtracting one because the navgraph itself is the first item on the stack
			int NativeNavigationStackCount = navController.BackStack.Size() - 1;

			// set this to one because when the graph is first attached to the controller
			// it will add the graph and the first destination
			if (NativeNavigationStackCount < 0)
				NativeNavigationStackCount = 1;

			List<int> destinations = new List<int>();

			NavDestination navDestination;

			foreach (var page in pages)
			{
				navDestination =
						AddDestination(
							page,
							navigationLayout);

				destinations.Add(navDestination.Id);
			}

			StartDestination = destinations[0];
			navController.SetGraph(this, null);

			for (var i = NativeNavigationStackCount; i < pages.Count; i++)
			{
				var dest = destinations[i];
				navController.Navigate(dest);
			}

			NavigationStack = new List<IView>(pages);
			return destinations;
		}
	}
}
