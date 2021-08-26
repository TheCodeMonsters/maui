using System;
using Android.Runtime;
using Android.Views;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Controls.Handlers
{
	// Currently only inheriting because we can't tap into CreateNativeView
	// Once we can wire up into CreateNativeView then all of this code can move into the 
	// Remap structures
	internal partial class NavigationPageHandler : Microsoft.Maui.Handlers.NavigationViewHandler
	{
		public new NavigationPageView NativeView =>
			(NavigationPageView)base.NativeView;

		public static PropertyMapper<NavigationPage, NavigationPageHandler> NavigationPageMapper =
			new PropertyMapper<NavigationPage, NavigationPageHandler>(NavigationViewHandler.NavigationViewMapper)
			{
				[nameof(NavigationPage.HasNavigationBarProperty.PropertyName)] = MapHasNavigationBar,
				[nameof(NavigationPage.HasBackButtonProperty.PropertyName)] = UpdateToolBar,
				[nameof(NavigationPage.TitleIconImageSourceProperty.PropertyName)] = UpdateToolBar,
				[nameof(NavigationPage.TitleViewProperty.PropertyName)] = UpdateToolBar,
				[nameof(NavigationPage.IconColorProperty.PropertyName)] = UpdateToolBar,
				[nameof(Page.TitleProperty.PropertyName)] = UpdateToolBar,
			};

		private static void MapHasNavigationBar(NavigationPageHandler arg1, NavigationPage arg2)
		{
			arg1.NativeView.ToolbarVisible = NavigationPage.GetHasNavigationBar(arg2.CurrentPage);
		}

		private static void UpdateToolBar(NavigationPageHandler arg1, NavigationPage arg2)
		{
			arg1.NativeView.ToolbarReady();
		}

		public NavigationPageHandler() : base(NavigationPageMapper)
		{

		}

		protected override NavigationLayout CreateNativeView()
		{
			LayoutInflater li = LayoutInflater.From(Context);
			_ = li ?? throw new InvalidOperationException($"LayoutInflater cannot be null");
			var view = li.Inflate(Resource.Layout.navigationlayoutcontrols, null).JavaCast<NavigationPageView>();
			return view;
		}

		public override void SetVirtualView(IView view)
		{
			base.SetVirtualView(view);
			NativeView.SetVirtualView((NavigationPage)view);
		}
	}
}
