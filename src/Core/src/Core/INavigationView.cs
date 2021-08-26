using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Maui
{
	public interface INavigationView : IView
	{
		IReadOnlyList<IView> NavigationStack { get; }
		void RequestNavigation(MauiNavigationRequestedEventArgs eventArgs);
		void NavigationFinished(IReadOnlyList<IView> newStack);
	}
}
