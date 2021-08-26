using System;
using System.Collections.Generic;
using System.Text;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.Animations;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.CoordinatorLayout.Widget;
using AndroidX.Fragment.App;
using AndroidX.Navigation.Fragment;
using AndroidX.Navigation.UI;
using Microsoft.Maui.Handlers;
using AView = Android.Views.View;

namespace Microsoft.Maui
{
	public class NavHostPageFragment : Fragment
	{
		NavigationLayout? _navigationLayout;
		NavigationLayout NavigationLayout => _navigationLayout ??= NavDestination.NavigationLayout;

		FragmentNavDestination? _navDestination;

		ProcessBackClick BackClick { get; }

		NavHostFragment NavHost =>
				   (Context?.GetFragmentManager()?.FindFragmentById(Resource.Id.nav_host)
			  as NavHostFragment) ?? throw new InvalidOperationException($"NavHost cannot be null here");

		NavGraphDestination Graph =>
				   (NavHost.NavController.Graph as NavGraphDestination) 
			?? throw new InvalidOperationException($"Graph cannot be null here");

		public FragmentNavDestination NavDestination
		{
			get => _navDestination ?? throw new InvalidOperationException($"NavDestination cannot be null here");
			private set => _navDestination = value;
		}

		protected NavHostPageFragment(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
			BackClick = new ProcessBackClick(this);
		}

		public NavHostPageFragment()
		{
			BackClick = new ProcessBackClick(this);
		}

		public override Animation OnCreateAnimation(int transit, bool enter, int nextAnim)
		{
			int id = 0;

			// TODO MAUI write comments about WHY
			if (Graph.IsPopping == null || !Graph.IsAnimated)
				return base.OnCreateAnimation(transit, enter, nextAnim);

			if (Graph.IsPopping.Value)
			{
				if (!enter)
				{
					id = Resource.Animation.exittoright;
				}
				else
				{
					id = Resource.Animation.enterfromleft;
				}
			}
			else
			{
				if (enter)
				{
					id = Resource.Animation.enterfromright;
				}
				else
				{
					id = Resource.Animation.exittoleft;
				}
			}

			var thing = AnimationUtils.LoadAnimation(Context, id);
			var animation =
				thing ?? base.OnCreateAnimation(transit, enter, id);
			
			return animation;
		}

		public override AView OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			if (_navDestination == null)
			{
				NavDestination =
					(FragmentNavDestination)
						NavHost.NavController.CurrentDestination;
			}

			_ = NavDestination ?? throw new ArgumentNullException(nameof(NavDestination));

			// TODO Maui can we tranplant the page better?
			// do we care?
			NavDestination.Page.Handler?.DisconnectHandler();
			NavDestination.Page.Handler = null;
			var view = NavDestination.Page.ToNative(NavDestination.MauiContext);

			return view;
		}

		public override void OnViewCreated(AView view, Bundle savedInstanceState)
		{
			base.OnViewCreated(view, savedInstanceState);

			Console.WriteLine($"OnViewCreated {(NavDestination.Page as ITitledElement)?.Title}");

			var controller = NavHostFragment.FindNavController(this);
			var appbarConfig =
				new AppBarConfiguration
					.Builder(controller.Graph)
					.Build();

			NavigationUI
				.SetupWithNavController(NavDestination.NavigationLayout.Toolbar, controller, appbarConfig);

			HasOptionsMenu = true;

			NavDestination.NavigationLayout.Toolbar.SetNavigationOnClickListener(BackClick);

			var titledElement = NavDestination.Page as ITitledElement;

			NavDestination.NavigationLayout.Toolbar.Title = titledElement?.Title;

			if (Context.GetActivity() is AppCompatActivity aca)
			{
			    //_navigationLayout.AppBar.action	aca.SupportActionBar.Title = titledElement?.Title;

				// TODO MAUI put this elsewhere once we figure out how attached property handlers work
				bool showNavBar = true;
				//if (NavDestination.Page is BindableObject bo)
				//	showNavBar = NavigationView.GetHasNavigationBar(bo);

				var appBar = NavDestination.NavigationLayout.AppBar;
				if (!showNavBar)
				{
					if (appBar.LayoutParameters is CoordinatorLayout.LayoutParams cl)
					{
						cl.Height = 0;
						appBar.LayoutParameters = cl;
					}
				}
				else
				{
					if (appBar.LayoutParameters is CoordinatorLayout.LayoutParams cl)
					{
						cl.Height = ActionBarHeight();
						appBar.LayoutParameters = cl;
					}
				}
			}


			int ActionBarHeight()
			{
				int attr = Resource.Attribute.actionBarSize;

				int actionBarHeight = (int)Context.GetThemeAttributePixels(Resource.Attribute.actionBarSize);

				//if (actionBarHeight <= 0)
				//	return Device.Info.CurrentOrientation.IsPortrait() ? (int)Context.ToPixels(56) : (int)Context.ToPixels(48);

				//if (Context.GetActivity().Window.Attributes.Flags.HasFlag(WindowManagerFlags.TranslucentStatus) || Context.GetActivity().Window.Attributes.Flags.HasFlag(WindowManagerFlags.TranslucentNavigation))
				//{
				//	if (_toolbar.PaddingTop == 0)
				//		_toolbar.SetPadding(0, GetStatusBarHeight(), 0, 0);

				//	return actionBarHeight + GetStatusBarHeight();
				//}

				return actionBarHeight;
			}

		}

		public override void OnResume()
		{
			base.OnResume();
		}


		public override void OnDestroyView()
		{			
			_navigationLayout = null;
			base.OnDestroyView();
		}

		public override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			RequireActivity()
				.OnBackPressedDispatcher
				.AddCallback(this, BackClick);
		}

		public void HandleOnBackPressed()
		{
			NavDestination.NavigationLayout.OnPop();
		}

		class ProcessBackClick : AndroidX.Activity.OnBackPressedCallback, AView.IOnClickListener
		{
			NavHostPageFragment _navHostPageFragment;

			public ProcessBackClick(NavHostPageFragment navHostPageFragment)
				: base(true)
			{
				_navHostPageFragment = navHostPageFragment;
			}

			public override void HandleOnBackPressed()
			{
				_navHostPageFragment.HandleOnBackPressed();
			}

			public void OnClick(AView? v)
			{
				HandleOnBackPressed();
			}
		}
	}
}
