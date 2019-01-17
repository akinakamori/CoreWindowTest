using System;
using System.Diagnostics;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace CoreWindowTest // <- プロジェクトにあわせる
{
	class App
	{
		[MTAThread]
		private static void Main()
		{
			var viewFactory = new FrameworkViewSource();
			CoreApplication.Run(viewFactory);
		}

		class FrameworkViewSource : IFrameworkViewSource
		{
			public IFrameworkView CreateView()
			{
				return new FrameworkView();
			}
		}

		class FrameworkView : IFrameworkView
		{
			CoreWindow m_window;
			SharpDX.DXGI.SwapChain1 m_swapChain;
			SharpDX.Direct3D11.Device1 m_d3dDevice;
			SharpDX.Direct3D11.DeviceContext1 m_d3dDeviceContext;
			SharpDX.Direct3D11.RenderTargetView m_renderTargetView;

			public void Initialize(CoreApplicationView applicationView)
			{
				Debug.WriteLine("Initialize");
				applicationView.Activated += OnActivated;
			}

			void OnActivated(
				 CoreApplicationView applicationView,
				 IActivatedEventArgs args
				 )
			{
				// Activate the application window, making it visible and enabling it to receive events.
				CoreWindow.GetForCurrentThread().Activate();
			}

			public void Load(string entryPoint)
			{
				Debug.WriteLine("Load: " + entryPoint);
			}

			public void Run()
			{
				Debug.WriteLine("Run");

				// First, create the Direct3D device.

				// This flag is required in order to enable compatibility with Direct2D.
				var creationFlags = SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport;

#if DEBUG
				// If the project is in a debug build, enable debugging via SDK Layers with this flag.
				creationFlags |= SharpDX.Direct3D11.DeviceCreationFlags.Debug;
#endif

				// This array defines the ordering of feature levels that D3D should attempt to create.
				var featureLevels = new SharpDX.Direct3D.FeatureLevel[]
			   {
					SharpDX.Direct3D.FeatureLevel.Level_11_1,
					SharpDX.Direct3D.FeatureLevel.Level_11_0,
			   };

				using (var d3dDevice = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware
					, creationFlags, featureLevels))
				{
					m_d3dDevice = d3dDevice.QueryInterface<SharpDX.Direct3D11.Device1>();
				}
				m_d3dDeviceContext = m_d3dDevice.ImmediateContext1;

				// After the D3D device is created, create additional application resources.
				CreateWindowSizeDependentResources();

				// Enter the render loop.  Note that Windows Store apps should never exit.
				while (true)
				{
					// Process events incoming to the window.
					m_window.Dispatcher.ProcessEvents(CoreProcessEventsOption.ProcessAllIfPresent);

					// Specify the render target we created as the output target.
					m_d3dDeviceContext.OutputMerger.SetRenderTargets(null,
						m_renderTargetView
						);

					// Clear the render target to a solid color.
					m_d3dDeviceContext.ClearRenderTargetView(
						m_renderTargetView,
						new SharpDX.Color4(0.071f, 0.04f, 0.561f, 1.0f)
						);

					// Present the rendered image to the window.  Because the maximum frame latency is set to 1,
					// the render loop will generally be throttled to the screen refresh rate, typically around
					// 60Hz, by sleeping the application on Present until the screen is refreshed.
					m_swapChain.Present(1, 0);
				}
			}

			public void SetWindow(CoreWindow window)
			{
				Debug.WriteLine("SetWindow: " + window);
				m_window = window;

				// Specify the cursor type as the standard arrow cursor.
				m_window.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);

				// Allow the application to respond when the window size changes.
				m_window.SizeChanged += OnWindowSizeChanged;
			}

			void OnWindowSizeChanged(
				CoreWindow sender,
				WindowSizeChangedEventArgs args
				)
			{
				m_renderTargetView = null;
				CreateWindowSizeDependentResources();
			}

			public void Uninitialize()
			{
				Debug.WriteLine("Uninitialize");
			}

			// This method creates all application resources that depend on
			// the application window size.  It is called at app initialization,
			// and whenever the application window size changes.
			void CreateWindowSizeDependentResources()
			{
				if (m_swapChain != null)
				{
					// If the swap chain already exists, resize it.
					m_swapChain.ResizeBuffers(
						2,
						0,
						0,
						SharpDX.DXGI.Format.B8G8R8A8_UNorm,
						0
					);
				}
				else
				{
					// If the swap chain does not exist, create it.
					var swapChainDesc = new SharpDX.DXGI.SwapChainDescription1
					{
						Stereo = false,
						Usage = SharpDX.DXGI.Usage.RenderTargetOutput,
						Scaling = SharpDX.DXGI.Scaling.None,
						Flags = 0,
					};

					// Use automatic sizing.
					swapChainDesc.Width = 0;
					swapChainDesc.Height = 0;

					// This is the most common swap chain format.
					swapChainDesc.Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm;

					// Don't use multi-sampling.
					swapChainDesc.SampleDescription.Count = 1;
					swapChainDesc.SampleDescription.Quality = 0;

					// Use two buffers to enable flip effect.
					swapChainDesc.BufferCount = 2;

					// We recommend using this swap effect for all applications.
					swapChainDesc.SwapEffect = SharpDX.DXGI.SwapEffect.FlipSequential;

					// Once the swap chain description is configured, it must be
					// created on the same adapter as the existing D3D Device.

					// First, retrieve the underlying DXGI Device from the D3D Device.
					using (var dxgiDevice = m_d3dDevice.QueryInterface<SharpDX.DXGI.Device2>())
					{

						// Ensure that DXGI does not queue more than one frame at a time. This both reduces
						// latency and ensures that the application will only render after each VSync, minimizing
						// power consumption.
						dxgiDevice.MaximumFrameLatency = 1;

						// Next, get the parent factory from the DXGI Device.
						using (var dxgiAdapter = dxgiDevice.Adapter)
						using (var dxgiFactory = dxgiAdapter.GetParent<SharpDX.DXGI.Factory2>())
						// Finally, create the swap chain.
						using (var coreWindow = new SharpDX.ComObject(m_window))
						{
							m_swapChain = new SharpDX.DXGI.SwapChain1(dxgiFactory
								, m_d3dDevice, coreWindow, ref swapChainDesc);
						}
					}
				}

				// Once the swap chain is created, create a render target view.  This will
				// allow Direct3D to render graphics to the window.
				using (var backBuffer = m_swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
				{
					m_renderTargetView = new SharpDX.Direct3D11.RenderTargetView(m_d3dDevice, backBuffer);

					// After the render target view is created, specify that the viewport,
					// which describes what portion of the window to draw to, should cover
					// the entire window.

					var backBufferDesc = backBuffer.Description;

					var viewport = new SharpDX.ViewportF
					{
						X = 0.0f,
						Y = 0.0f,
						Width = backBufferDesc.Width,
						Height = backBufferDesc.Height,
						MinDepth = 0,
						MaxDepth = 1,
					};

					m_d3dDeviceContext.Rasterizer.SetViewport(viewport);
				}
			}
		}
	}

}
