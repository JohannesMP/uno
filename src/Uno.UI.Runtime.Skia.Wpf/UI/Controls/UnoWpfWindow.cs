﻿#nullable enable

using System;
using System.ComponentModel;
using Windows.UI.Core.Preview;
using WinUI = Windows.UI.Xaml;
using WpfWindow = System.Windows.Window;
using WinUIApplication = Windows.UI.Xaml.Application;
using Windows.UI.ViewManagement;
using Uno.Foundation.Logging;
using System.IO;

namespace Uno.UI.Runtime.Skia.Wpf.UI.Controls;

internal class UnoWpfWindow : WpfWindow
{
	private readonly WinUI.Window _winUIWindow;
	private bool _isVisible;

	public UnoWpfWindow(WinUI.Window winUIWindow)
	{
		_winUIWindow = winUIWindow ?? throw new ArgumentNullException(nameof(winUIWindow));
		_winUIWindow.Shown += OnShown;

		Windows.Foundation.Size preferredWindowSize = ApplicationView.PreferredLaunchViewSize;
		if (preferredWindowSize != Windows.Foundation.Size.Empty)
		{
			Width = (int)preferredWindowSize.Width;
			Height = (int)preferredWindowSize.Height;
		}

		Content = new UnoWpfWindowHost(this, winUIWindow);

		Closing += OnClosing;
		Activated += OnActivated;
		Deactivated += OnDeactivated;
		StateChanged += OnStateChanged;

		UpdateWindowPropertiesFromPackage();
	}

	private void OnShown(object? sender, EventArgs e) => Show();

	private void OnClosing(object? sender, CancelEventArgs e)
	{
		// TODO: Support multi-window approach properly #8341
		var manager = SystemNavigationManagerPreview.GetForCurrentView();
		if (!manager.HasConfirmedClose)
		{
			if (!manager.RequestAppClose())
			{
				e.Cancel = true;
				return;
			}
		}

		// Closing should continue, perform suspension.
		WinUIApplication.Current.RaiseSuspending();
	}

	private void OnDeactivated(object? sender, EventArgs e) =>
		_winUIWindow?.RaiseActivated(Windows.UI.Core.CoreWindowActivationState.Deactivated);

	private void OnActivated(object? sender, EventArgs e) =>
		_winUIWindow.RaiseActivated(Windows.UI.Core.CoreWindowActivationState.PointerActivated);

	private void OnStateChanged(object? sender, EventArgs e)
	{
		var application = WinUIApplication.Current;
		var wasVisible = _isVisible;

		_isVisible = WindowState != System.Windows.WindowState.Minimized;

		if (wasVisible && !_isVisible)
		{
			_winUIWindow.OnVisibilityChanged(false);
			application?.RaiseEnteredBackground(null);
		}
		else if (!wasVisible && _isVisible)
		{
			application?.RaiseLeavingBackground(() => _winUIWindow?.OnVisibilityChanged(true));
		}
	}


	private void UpdateWindowPropertiesFromPackage()
	{
		if (Windows.ApplicationModel.Package.Current.Logo is Uri uri)
		{
			var basePath = uri.OriginalString.Replace('\\', Path.DirectorySeparatorChar);
			var iconPath = Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath, basePath);

			if (File.Exists(iconPath))
			{
				if (this.Log().IsEnabled(LogLevel.Information))
				{
					this.Log().Info($"Loading icon file [{iconPath}] from Package.appxmanifest file");
				}

				Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
			}
			else if (Windows.UI.Xaml.Media.Imaging.BitmapImage.GetScaledPath(basePath) is { } scaledPath && File.Exists(scaledPath))
			{
				if (this.Log().IsEnabled(LogLevel.Information))
				{
					this.Log().Info($"Loading icon file [{scaledPath}] scaled logo from Package.appxmanifest file");
				}

				Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(scaledPath));
			}
			else
			{
				if (this.Log().IsEnabled(LogLevel.Warning))
				{
					this.Log().Warn($"Unable to find icon file [{iconPath}] specified in the Package.appxmanifest file.");
				}
			}
		}

		Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().Title = Windows.ApplicationModel.Package.Current.DisplayName;
	}
}
