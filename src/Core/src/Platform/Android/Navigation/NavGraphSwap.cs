//using System;
//using System.Collections.Generic;
//using System.Text;
//using AndroidX.Navigation;

//namespace Microsoft.Maui
//{
//	public class NavGraphSwap : NavGraph
//	{
//		// TODO MAUI make this behavior internal to this 
//		public NavGraphDestination Active { get; set; }
//		public NavGraphDestination InActive { get; set; }

//		public NavGraphSwap(Navigator navGraphNavigator) : base(navGraphNavigator)
//		{
//			Active = new NavGraphDestination(navGraphNavigator);
//			InActive = new NavGraphDestination(navGraphNavigator);

//			this.AddDestinations(Active, InActive);

//			StartDestination = Active.Id;
//		}

//		public void SwapGraphs()
//		{
//			var thing = Active;
//			Active = InActive;
//			InActive = thing;
//		}
//	}
//}
