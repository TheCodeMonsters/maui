using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Android.Animation;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Runtime;
using Android.Util;
using Android.Views;
using AndroidX.AppCompat.Graphics.Drawable;
using AndroidX.DrawerLayout.Widget;
using Google.Android.Material.AppBar;
using Microsoft.Maui.Controls.Internals;
using static Android.Views.View;
using static Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.AppCompat.NavigationPage;
using ActionBarDrawerToggle = AndroidX.AppCompat.App.ActionBarDrawerToggle;
using AToolbar = AndroidX.AppCompat.Widget.Toolbar;
using AView = Android.Views.View;
using Color = Microsoft.Maui.Graphics.Color;
using FragmentManager = AndroidX.Fragment.App.FragmentManager;
using FragmentTransaction = AndroidX.Fragment.App.FragmentTransaction;
using Object = Java.Lang.Object;

namespace Microsoft.Maui.Controls.Platform
{
	public class NavigationPageView : NavigationLayout, IManageFragments, IOnClickListener
	{
		ActionBarDrawerToggle _drawerToggle;
		FragmentManager _fragmentManager;
		//int _statusbarHeight;
		MaterialToolbar _toolbar;
		AppBarLayout _appBar;
		ToolbarTracker _toolbarTracker;
		DrawerMultiplexedListener _drawerListener;
		DrawerLayout _drawerLayout;
		FlyoutPage _flyoutPage;
		bool _toolbarVisible;
		IViewHandler _titleViewHandler;
		Container _titleView;
		Android.Widget.ImageView _titleIconView;
		ImageSource _imageSource;
		//bool _isAttachedToWindow;
		List<IMenuItem> _currentMenuItems = new List<IMenuItem>();
		List<ToolbarItem> _currentToolbarItems = new List<ToolbarItem>();

		// The following is based on https://android.googlesource.com/platform/frameworks/support.git/+/4a7e12af4ec095c3a53bb8481d8d92f63157c3b7/v4/java/android/support/v4/app/FragmentManager.java#677
		// Must be overriden in a custom renderer to match durations in XML animation resource files
		protected virtual int TransitionDuration { get; set; } = 220;

		NavigationPage Element { get; set; }

		public NavigationPageView(Context context) : base(context)
		{
			Id = AView.GenerateViewId();
		}

		public NavigationPageView(Context context, IAttributeSet attrs) : base(context, attrs)
		{
		}

		public NavigationPageView(Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
		{
		}

		protected NavigationPageView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
		}

		INavigationPageController NavigationPageController => Element as INavigationPageController;

		IPageController PageController => Element;

		internal bool ToolbarVisible
		{
			get { return _toolbarVisible; }
			set
			{
				if (_toolbarVisible == value)
					return;

				_toolbarVisible = value;

				if (!IsLayoutRequested)
					RequestLayout();
			}
		}

		void IManageFragments.SetFragmentManager(FragmentManager childFragmentManager)
		{
			if (_fragmentManager == null)
				_fragmentManager = childFragmentManager;
		}

		internal void SetVirtualView(NavigationPage view)
		{
			Element = view;
			if (_toolbarTracker == null)
			{
				_toolbar = FindViewById<MaterialToolbar>(Resource.Id.maui_toolbar);
				_appBar = FindViewById<AppBarLayout>(Resource.Id.appbar);
				_toolbarTracker = new ToolbarTracker();
				_toolbarTracker.CollectionChanged += ToolbarTrackerOnCollectionChanged;
			}
			_toolbarTracker.AdditionalTargets = Element.GetParentPages();
		}

		void AnimateArrowIn()
		{
			var icon = _toolbar.NavigationIcon as DrawerArrowDrawable;
			if (icon == null)
				return;

			ValueAnimator valueAnim = ValueAnimator.OfFloat(0, 1);
			valueAnim.SetDuration(200);
			valueAnim.Update += (s, a) => icon.Progress = (float)a.Animation.AnimatedValue;
			valueAnim.Start();
		}

		void AnimateArrowOut()
		{
			var icon = _toolbar.NavigationIcon as DrawerArrowDrawable;
			if (icon == null)
				return;

			ValueAnimator valueAnim = ValueAnimator.OfFloat(1, 0);
			valueAnim.SetDuration(200);
			valueAnim.Update += (s, a) => icon.Progress = (float)a.Animation.AnimatedValue;
			valueAnim.Start();
		}

		public void OnClick(AView v)
		{
			Element?.PopAsync();
		}

		public override void RequestNavigation(MauiNavigationRequestedEventArgs e)
		{
			NavAnimationInProgress = true;
			base.RequestNavigation(e);
			NavAnimationInProgress = false;

			var currentStack = NavGraphDestination.NavigationStack;
			bool animated = e.Animated;
			bool removed = NavGraphDestination.NavigationStack.Count > e.NavigationStack.Count;

			if (animated)
			{
				var page = (Page)e.NavigationStack.Last();
				if (!removed)
				{
					UpdateToolbar();
					if (_drawerToggle != null && NavigationPageController.StackDepth == 2 && 
						NavigationPage.GetHasBackButton(page))
						AnimateArrowIn();
				}
				else if (_drawerToggle != null && NavigationPageController.StackDepth == 2 && 
					NavigationPage.GetHasBackButton(page))
					AnimateArrowOut();
			}
		}

		private protected override void OnPageFragmentDestroyed(FragmentManager fm, NavHostPageFragment navHostPageFragment)
		{
			base.OnPageFragmentDestroyed(fm, navHostPageFragment);
			UpdateToolbar();
		}

		protected private override void OnFragmentResumed(FragmentManager fm, NavHostPageFragment navHostPageFragment)
		{
			base.OnFragmentResumed(fm, navHostPageFragment);
			_toolbarTracker.Target = (Page)navHostPageFragment.NavDestination.Page;
			UpdateToolbar();
		}

		void OnPushed(object sender, NavigationRequestedEventArgs e)
		{
			//e.Task = PushViewAsync(e.Page, e.Animated);
		}

		//void OnRemovePageRequested(object sender, NavigationRequestedEventArgs e)
		//{
		//	RemovePage(e.Page);
		//}

		void RegisterToolbar()
		{
			Context context = Context;
			AToolbar bar = _toolbar;
			Element page = Element.RealParent;

			_flyoutPage = null;
			while (page != null)
			{
				if (page is FlyoutPage)
				{
					_flyoutPage = page as FlyoutPage;
					break;
				}
				page = page.RealParent;
			}

			if (_flyoutPage == null)
			{
				if (PageController.InternalChildren.Count > 0)
					_flyoutPage = PageController.InternalChildren[0] as FlyoutPage;

				if (_flyoutPage == null)
					return;
			}

			if (((IFlyoutPageController)_flyoutPage).ShouldShowSplitMode)
				return;

			var renderer = _flyoutPage.ToNative(Element.Handler.MauiContext) as DrawerLayout;
			if (renderer == null)
				return;

			_drawerLayout = renderer;

			AutomationPropertiesProvider.GetDrawerAccessibilityResources(context, _flyoutPage, out int resourceIdOpen, out int resourceIdClose);

			if (_drawerToggle != null)
			{
				_drawerToggle.ToolbarNavigationClickListener = null;
				_drawerToggle.Dispose();
			}

			_drawerToggle = new ActionBarDrawerToggle(context.GetActivity(), _drawerLayout, bar,
				resourceIdOpen == 0 ? global::Android.Resource.String.Ok : resourceIdOpen,
				resourceIdClose == 0 ? global::Android.Resource.String.Ok : resourceIdClose)
			{
				ToolbarNavigationClickListener = new ClickListener(Element)
			};

			if (_drawerListener != null)
			{
				_drawerLayout.RemoveDrawerListener(_drawerListener);
				_drawerListener.Dispose();
			}

			_drawerListener = new DrawerMultiplexedListener { Listeners = { _drawerToggle, (DrawerLayout.IDrawerListener)_drawerLayout } };
			_drawerLayout.AddDrawerListener(_drawerListener);
		}


		// AFAICT this is specific to ListView and Context Items
		bool _navAnimationInProgress;
		internal const string CloseContextActionsSignalName = "Xamarin.CloseContextActions";
		internal bool NavAnimationInProgress
		{
			get { return _navAnimationInProgress; }
			set
			{
				if (_navAnimationInProgress == value)
					return;
				_navAnimationInProgress = value;
				if (value)
					MessagingCenter.Send(this, CloseContextActionsSignalName);
			}
		}

		void ToolbarTrackerOnCollectionChanged(object sender, EventArgs eventArgs)
		{
			UpdateMenu();
		}

		void UpdateMenu()
		{
			if (_currentMenuItems == null)
				return;

			_toolbar.UpdateMenuItems(_toolbarTracker?.ToolbarItems, Element.FindMauiContext(), null, OnToolbarItemPropertyChanged, _currentMenuItems, _currentToolbarItems, UpdateMenuItemIcon);
		}

		protected virtual void OnToolbarItemPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var items = _toolbarTracker?.ToolbarItems?.ToList();
			_toolbar.OnToolbarItemPropertyChanged(e, (ToolbarItem)sender, items, Element.FindMauiContext(), null, OnToolbarItemPropertyChanged, _currentMenuItems, _currentToolbarItems, UpdateMenuItemIcon);
		}

		protected virtual void UpdateMenuItemIcon(Context context, IMenuItem menuItem, ToolbarItem toolBarItem)
		{
			ToolbarExtensions.UpdateMenuItemIcon(Element.FindMauiContext(), menuItem, toolBarItem, null);
		}

		private protected override void UpdateToolbar()
		{
			ActionBarDrawerToggle toggle = _drawerToggle;

			if (_toolbar == null)
				return;

			bool isNavigated = NavigationPageController.StackDepth > 1;
			Page currentPage = Element.CurrentPage;

			if (isNavigated)
			{
				if (NavigationPage.GetHasBackButton(currentPage))
				{
					if (toggle != null)
					{
						toggle.DrawerIndicatorEnabled = false;
						toggle.SyncState();
					}
					
					var prevPage = Element.Peek(1);
					var backButtonTitle = NavigationPage.GetBackButtonTitle(prevPage);

					ImageSource image = NavigationPage.GetTitleIconImageSource(currentPage);
					if (!string.IsNullOrEmpty(backButtonTitle))
					{
						_toolbar.NavigationContentDescription = backButtonTitle;
					}
					else if (image == null ||
						_toolbar.SetNavigationContentDescription(image) == null)
					{
						_toolbar.SetNavigationContentDescription(Resource.String.nav_app_bar_navigate_up_description);
					}
				}
				else if (toggle != null && _flyoutPage != null)
				{
					toggle.DrawerIndicatorEnabled = _flyoutPage.ShouldShowToolbarButton();
					toggle.SyncState();
				}
			}
			else
			{
				if (toggle != null && _flyoutPage != null)
				{
					toggle.DrawerIndicatorEnabled = _flyoutPage.ShouldShowToolbarButton();
					toggle.SyncState();
					_toolbar.SetNavigationContentDescription(Resource.String.nav_app_bar_open_drawer_description);
				}
			}

			Color tintColor = Element.BarBackgroundColor;

			if (tintColor == null)
				_toolbar.BackgroundTintMode = null;
			else
			{
				_toolbar.BackgroundTintMode = PorterDuff.Mode.Src;
				_toolbar.BackgroundTintList = ColorStateList.ValueOf(tintColor.ToNative());
			}

			Brush barBackground = Element.BarBackground;
			_toolbar.UpdateBackground(barBackground);

			Color textColor = Element.BarTextColor;
			if (textColor != null)
				_toolbar.SetTitleTextColor(textColor.ToNative().ToArgb());

			Color navIconColor = NavigationPage.GetIconColor(Element.CurrentPage);
			if (navIconColor != null && _toolbar.NavigationIcon != null)
				DrawableExtensions.SetColorFilter(_toolbar.NavigationIcon, navIconColor, FilterMode.SrcAtop);

			_toolbar.Title = currentPage?.Title ?? string.Empty;

			if (_toolbar.NavigationIcon != null && textColor != null)
			{
				var icon = this._toolbar.NavigationIcon as DrawerArrowDrawable;
				if (icon != null)
					icon.Color = textColor.ToNative().ToArgb();
			}

			UpdateTitleIcon();
			UpdateTitleView();
		}

		void UpdateTitleIcon()
		{
			Page currentPage = Element.CurrentPage;

			if (currentPage == null)
				return;

			ImageSource source = NavigationPage.GetTitleIconImageSource(currentPage);

			if (source == null || source.IsEmpty)
			{
				_toolbar.RemoveView(_titleIconView);
				_titleIconView?.Dispose();
				_titleIconView = null;
				_imageSource = null;
				return;
			}

			if (_titleIconView == null)
			{
				_titleIconView = new Android.Widget.ImageView(Context);
				_toolbar.AddView(_titleIconView, 0);
			}

			if (_imageSource != source)
			{
				_imageSource = source;
				_titleIconView.SetImageResource(global::Android.Resource.Color.Transparent);

				ImageSourceLoader.LoadImage(source, MauiContext, (result) =>
				{
					_titleIconView.SetImageDrawable(result.Value);
					AutomationPropertiesProvider.AccessibilitySettingsChanged(_titleIconView, source);
				});
			}
		}

		void UpdateTitleView()
		{
			AToolbar bar = _toolbar;

			if (bar == null)
				return;

			Page currentPage = Element.CurrentPage;

			if (currentPage == null)
				return;

			VisualElement titleView = NavigationPage.GetTitleView(currentPage);
			if (_titleViewHandler != null)
			{
				var reflectableType = _titleViewHandler as System.Reflection.IReflectableType;
				var rendererType = reflectableType != null ? reflectableType.GetTypeInfo().AsType() : _titleViewHandler.GetType();
				if (titleView == null || Internals.Registrar.Registered.GetHandlerTypeForObject(titleView) != rendererType)
				{
					if (_titleView != null)
						_titleView.Child = null;

					_titleViewHandler.VirtualView.Handler = null;
					_titleViewHandler = null;
				}
			}

			if (titleView == null)
				return;

			if (_titleViewHandler != null)
				_titleViewHandler.SetVirtualView(titleView);
			else
			{
				titleView.ToNative(MauiContext);
				_titleViewHandler = titleView.Handler;

				if (_titleView == null)
				{
					_titleView = new Container(Context);
					bar.AddView(_titleView);
				}

				_titleView.Child = (INativeViewHandler)_titleViewHandler;
			}
		}

		class ClickListener : Object, IOnClickListener
		{
			readonly NavigationPage _element;

			public ClickListener(NavigationPage element)
			{
				_element = element;
			}

			public void OnClick(AView v)
			{
				_element?.PopAsync();
			}
		}

		internal class Container : ViewGroup
		{
			INativeViewHandler _child;

			public Container(Context context) : base(context)
			{
			}

			public INativeViewHandler Child
			{
				set
				{
					if (_child != null)
						RemoveView(_child.NativeView);

					_child = value;

					if (value != null)
						AddView(value.NativeView);
				}
			}

			protected override void OnLayout(bool changed, int l, int t, int r, int b)
			{
				if (_child == null)
					return;

				_child.NativeView.Layout(l, t, r, b);
			}

			protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
			{
				if (_child == null)
				{
					SetMeasuredDimension(0, 0);
					return;
				}

				_child.NativeView.Measure(widthMeasureSpec, heightMeasureSpec);
				SetMeasuredDimension(_child.NativeView.MeasuredWidth, _child.NativeView.MeasuredHeight);
			}
		}

		class DrawerMultiplexedListener : Object, DrawerLayout.IDrawerListener
		{
			public List<DrawerLayout.IDrawerListener> Listeners { get; } = new List<DrawerLayout.IDrawerListener>(2);

			public void OnDrawerClosed(AView drawerView)
			{
				foreach (DrawerLayout.IDrawerListener listener in Listeners)
					listener.OnDrawerClosed(drawerView);
			}

			public void OnDrawerOpened(AView drawerView)
			{
				foreach (DrawerLayout.IDrawerListener listener in Listeners)
					listener.OnDrawerOpened(drawerView);
			}

			public void OnDrawerSlide(AView drawerView, float slideOffset)
			{
				foreach (DrawerLayout.IDrawerListener listener in Listeners)
					listener.OnDrawerSlide(drawerView, slideOffset);
			}

			public void OnDrawerStateChanged(int newState)
			{
				foreach (DrawerLayout.IDrawerListener listener in Listeners)
					listener.OnDrawerStateChanged(newState);
			}
		}
	}
}