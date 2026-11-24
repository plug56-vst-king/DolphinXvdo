using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Text;
using System.Text.RegularExpressions;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("2.3.0.0")]
[assembly: AssemblyFileVersion("2.3.0.0")]
namespace DolphinXVDO
{
	internal static class Program
	{
		[DllImport("shell32.dll", SetLastError = true)]
		private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern int RegisterWindowMessage(string lpString);

		[DllImport("user32.dll")]
		private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

		public static readonly int WM_SHOW_DOLPHINXVDO = RegisterWindowMessage("DolphinXvdo_ShowInstance_Message");

		private static Mutex singleInstanceMutex = null;

		[STAThread]
		private static void Main()
		{
			bool isFirstInstance = false;
			try
			{
				singleInstanceMutex = new Mutex(true, "Global\\DolphinXvdo_SingleInstance_Mutex_2026", out isFirstInstance);
			}
			catch
			{
				try
				{
					singleInstanceMutex = new Mutex(true, "DolphinXvdo_SingleInstance_Mutex_2026", out isFirstInstance);
				}
				catch
				{
					isFirstInstance = true;
				}
			}

			if (!isFirstInstance)
			{
				// Another instance of DolphinXvdo is already running!
				// If this instance was launched normally (not --minimized), signal the existing instance to pop up and focus
				bool startMinimizedArg = false;
				string[] commandLineArgs2 = Environment.GetCommandLineArgs();
				if (commandLineArgs2 != null)
				{
					foreach (string text in commandLineArgs2)
					{
						if (text.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || text.Equals("-minimized", StringComparison.OrdinalIgnoreCase) || text.Equals("/minimized", StringComparison.OrdinalIgnoreCase))
						{
							startMinimizedArg = true;
						}
					}
				}
				if (!startMinimizedArg && WM_SHOW_DOLPHINXVDO != 0)
				{
					try
					{
						PostMessage((IntPtr)0xffff, WM_SHOW_DOLPHINXVDO, IntPtr.Zero, IntPtr.Zero);
					}
					catch
					{
					}
				}
				return;
			}

			try
			{
				SetCurrentProcessExplicitAppUserModelID("DolphinXvdo.AudioController.App.v177");
			}
			catch
			{
			}
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
			{
				LogException(e.ExceptionObject as Exception);
			};
			Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
			{
				LogException(e.Exception);
			};
			bool startMinimized = false;
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			if (commandLineArgs != null)
			{
				string[] array = commandLineArgs;
				foreach (string text in array)
				{
					if (text.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || text.Equals("-minimized", StringComparison.OrdinalIgnoreCase) || text.Equals("/minimized", StringComparison.OrdinalIgnoreCase))
					{
						startMinimized = true;
					}
				}
			}
			try
			{
				Application.Run(new MainForm(startMinimized));
			}
			catch (Exception ex)
			{
				LogException(ex);
			}
			finally
			{
				if (singleInstanceMutex != null)
				{
					try
					{
						singleInstanceMutex.ReleaseMutex();
						singleInstanceMutex.Dispose();
					}
					catch
					{
					}
					singleInstanceMutex = null;
				}
			}
		}

		private static void LogException(Exception ex)
		{
			if (ex != null)
			{
				try
				{
					string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
					File.AppendAllText(path, string.Format("[{0}] {1}\r\n{2}\r\n\r\n", DateTime.Now, ex.Message, ex.ToString()));
					MessageBox.Show("An unexpected error occurred:\n" + ex.Message + "\n\n(Details saved to crash.log)", "DolphinXvdo v2.3 Crash", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
				catch
				{
				}
			}
		}
	}
	public class BiquadFilter
	{
		private const double FS = 48000.0;

		public double b0;

		public double b1;

		public double b2;

		public double a1;

		public double a2;

		public string Type
		{
			get;
			set;
		}

		public double Frequency
		{
			get;
			set;
		}

		public double Gain
		{
			get;
			set;
		}

		public double Q
		{
			get;
			set;
		}

		public BiquadFilter(string type, double fc, double gain, double q)
		{
			Type = NormalizeType(type);
			Frequency = fc;
			Gain = gain;
			Q = q;
			CalculateCoefficients();
		}

		public static string NormalizeType(string t)
		{
			if (string.IsNullOrEmpty(t))
			{
				return "PK";
			}
			t = t.Trim().ToUpperInvariant();
			if (t == "LS" || t == "LOWSHELF" || t == "LSC")
			{
				return "LS";
			}
			if (t == "HS" || t == "HIGHSHELF" || t == "HSC")
			{
				return "HS";
			}
			if (t == "HP" || t == "HIGHPASS" || t == "HPQ")
			{
				return "HP";
			}
			return "PK";
		}

		public void CalculateCoefficients()
		{
			Type = NormalizeType(Type);
			if (Q < 0.05)
			{
				Q = 0.05;
			}
			if (Frequency < 20.0)
			{
				Frequency = 20.0;
			}
			if (Frequency > 22000.0)
			{
				Frequency = 22000.0;
			}
			double num = Math.PI * 2.0 * Frequency / 48000.0;
			double num2 = Math.Cos(num);
			double num3 = Math.Sin(num);
			double num4 = Math.Pow(10.0, Gain / 40.0);
			double num5 = num3 / (2.0 * Q);
			double num6 = 1.0;
			double num7 = 0.0;
			double num8 = 0.0;
			double num9 = 1.0;
			double num10 = 0.0;
			double num11 = 0.0;
			switch (Type)
			{
			case "LS":
			{
				double num12 = 2.0 * Math.Sqrt(num4) * num5;
				num6 = num4 * (num4 + 1.0 - (num4 - 1.0) * num2 + num12);
				num7 = 2.0 * num4 * (num4 - 1.0 - (num4 + 1.0) * num2);
				num8 = num4 * (num4 + 1.0 - (num4 - 1.0) * num2 - num12);
				num9 = num4 + 1.0 + (num4 - 1.0) * num2 + num12;
				num10 = -2.0 * (num4 - 1.0 + (num4 + 1.0) * num2);
				num11 = num4 + 1.0 + (num4 - 1.0) * num2 - num12;
				break;
			}
			case "HS":
			{
				double num12 = 2.0 * Math.Sqrt(num4) * num5;
				num6 = num4 * (num4 + 1.0 + (num4 - 1.0) * num2 + num12);
				num7 = -2.0 * num4 * (num4 - 1.0 + (num4 + 1.0) * num2);
				num8 = num4 * (num4 + 1.0 + (num4 - 1.0) * num2 - num12);
				num9 = num4 + 1.0 - (num4 - 1.0) * num2 + num12;
				num10 = 2.0 * (num4 - 1.0 - (num4 + 1.0) * num2);
				num11 = num4 + 1.0 - (num4 - 1.0) * num2 - num12;
				break;
			}
			case "HP":
				num6 = (1.0 + num2) / 2.0;
				num7 = 0.0 - (1.0 + num2);
				num8 = (1.0 + num2) / 2.0;
				num9 = 1.0 + num5;
				num10 = -2.0 * num2;
				num11 = 1.0 - num5;
				break;
			default:
				num6 = 1.0 + num5 * num4;
				num7 = -2.0 * num2;
				num8 = 1.0 - num5 * num4;
				num9 = 1.0 + num5 / num4;
				num10 = -2.0 * num2;
				num11 = 1.0 - num5 / num4;
				break;
			}
			b0 = num6 / num9;
			b1 = num7 / num9;
			b2 = num8 / num9;
			a1 = num10 / num9;
			a2 = num11 / num9;
		}

		public double GetMagnitudeDbAt(double freq)
		{
			if (Type == "PK" && Math.Abs(Gain) < 0.01)
			{
				return 0.0;
			}
			if ((Type == "LS" || Type == "HS") && Math.Abs(Gain) < 0.01)
			{
				return 0.0;
			}
			double num = Math.PI * 2.0 * freq / 48000.0;
			double num2 = Math.Cos(num);
			double num3 = Math.Sin(num);
			double num4 = Math.Cos(2.0 * num);
			double num5 = Math.Sin(2.0 * num);
			double num6 = b0 + b1 * num2 + b2 * num4;
			double num7 = 0.0 - (b1 * num3 + b2 * num5);
			double num8 = 1.0 + a1 * num2 + a2 * num4;
			double num9 = 0.0 - (a1 * num3 + a2 * num5);
			double num10 = num6 * num6 + num7 * num7;
			double num11 = num8 * num8 + num9 * num9;
			if (num11 < 1E-12)
			{
				return 0.0;
			}
			double num12 = 10.0 * Math.Log10(num10 / num11);
			return (double.IsNaN(num12) || double.IsInfinity(num12)) ? 0.0 : num12;
		}
	}
	internal static class UiTheme
	{
		public static readonly Color BgCanvas = Color.FromArgb(0, 0, 0);
		public static readonly Color BgCard = Color.FromArgb(10, 10, 10);
		public static readonly Color BgWell = Color.FromArgb(18, 18, 18);
		public static readonly Color BgChip = Color.FromArgb(31, 31, 31);
		public static readonly Color BgHover = Color.FromArgb(28, 28, 28);
		public static readonly Color BgPressed = Color.FromArgb(38, 38, 38);
		public static readonly Color BorderRing = Color.FromArgb(37, 255, 255, 255);
		public static readonly Color BorderRingSolid = Color.FromArgb(46, 46, 46);
		public static readonly Color BorderSubtle = Color.FromArgb(22, 255, 255, 255);
		public static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
		public static readonly Color TextBody = Color.FromArgb(237, 237, 237);
		public static readonly Color TextMuted = Color.FromArgb(153, 153, 153);
		public static readonly Color TextDim = Color.FromArgb(108, 108, 108);
		public static readonly Color AccentBlue = Color.FromArgb(82, 168, 255);
		public static readonly Color AccentGreen = Color.FromArgb(98, 192, 115);
		public static readonly Color AccentAlert = Color.FromArgb(237, 237, 237);
		public static readonly Color CtaBg = Color.FromArgb(255, 255, 255);
		public static readonly Color CtaHoverBg = Color.FromArgb(232, 232, 232);
		public static readonly Color CtaPressedBg = Color.FromArgb(210, 210, 210);
		public static readonly Color CtaText = Color.FromArgb(18, 18, 18);

		private static string displayFamily;
		private static string monoFamily;

		static UiTheme()
		{
			displayFamily = ResolveFont(new string[] { "Inter Display", "Inter", "Segoe UI Variable Display", "Segoe UI" }, "Segoe UI");
			monoFamily = ResolveFont(new string[] { "Geist Mono", "Cascadia Mono", "Consolas" }, "Consolas");
		}

		private static string ResolveFont(string[] candidates, string fallback)
		{
			foreach (string name in candidates)
			{
				try
				{
					using (Font f = new Font(name, 9f, FontStyle.Regular))
					{
						if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
						{
							return name;
						}
					}
				}
				catch
				{
				}
			}
			return fallback;
		}

		public static Font DisplayFont(float size, FontStyle style = FontStyle.Regular)
		{
			return new Font(displayFamily, size, style);
		}

		public static Font MonoFont(float size, FontStyle style = FontStyle.Regular)
		{
			return new Font(monoFamily, size, style);
		}

		public static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
		{
			GraphicsPath path = new GraphicsPath();
			if (bounds.Width <= 0f || bounds.Height <= 0f)
			{
				return path;
			}
			float maxR = Math.Min(bounds.Width, bounds.Height) / 2f;
			float r = Math.Max(0.5f, Math.Min(radius, maxR));
			float d = r * 2f;
			path.AddArc(bounds.X, bounds.Y, d, d, 180f, 90f);
			path.AddArc(bounds.Right - d, bounds.Y, d, d, 270f, 90f);
			path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0f, 90f);
			path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90f, 90f);
			path.CloseFigure();
			return path;
		}

		public static void DrawRoundedSurface(Graphics g, int width, int height, float radius, Color fill, Color border, float borderWidth = 1f)
		{
			if (width <= 2 || height <= 2)
			{
				return;
			}
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			RectangleF rect = new RectangleF(0.5f, 0.5f, (float)width - 2f, (float)height - 2f);
			using (GraphicsPath path = CreateRoundedPath(rect, radius))
			{
				using (SolidBrush br = new SolidBrush(fill))
				{
					g.FillPath(br, path);
				}
				if (border.A > 0 && borderWidth > 0f)
				{
					using (Pen pen = new Pen(border, borderWidth))
					{
						g.DrawPath(pen, path);
					}
				}
			}
		}
	}

	public class ModernStatusPill : Control
	{
		public Color DotColor { get; set; }
		public Color PillBackColor { get; set; }
		public Color PillBorderColor { get; set; }
		public Color PillTextColor { get; set; }

		public ModernStatusPill(string text)
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			BackColor = Color.Transparent;
			Text = text;
			Font = UiTheme.MonoFont(7.6f, FontStyle.Bold);
			DotColor = UiTheme.AccentGreen;
			PillBackColor = UiTheme.BgChip;
			PillBorderColor = UiTheme.BorderRing;
			PillTextColor = UiTheme.TextMuted;
			Size = new Size(56, 22);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			RectangleF rect = new RectangleF(0.5f, 0.5f, (float)Width - 2f, (float)Height - 2f);
			float radius = rect.Height / 2f;
			using (GraphicsPath path = UiTheme.CreateRoundedPath(rect, radius))
			{
				using (SolidBrush br = new SolidBrush(PillBackColor))
				{
					g.FillPath(br, path);
				}
				using (Pen pen = new Pen(PillBorderColor, 1f))
				{
					g.DrawPath(pen, path);
				}
			}
			float dotSize = 6f;
			float dotX = 9f;
			float dotY = ((float)Height - dotSize) / 2f;
			using (SolidBrush dotBr = new SolidBrush(DotColor))
			{
				g.FillEllipse(dotBr, dotX, dotY, dotSize, dotSize);
			}
			using (SolidBrush txtBr = new SolidBrush(PillTextColor))
			using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
			{
				RectangleF txtRect = new RectangleF(dotX + dotSize + 5f, 0f, (float)Width - (dotX + dotSize + 10f), (float)Height);
				g.DrawString(Text, Font, txtBr, txtRect, sf);
			}
		}
	}

	public class ModernNumberBox : Panel
	{
		private Color normalBorderColor = UiTheme.BorderRing;

		private Color focusedBorderColor = UiTheme.AccentBlue;

		private Color hoverBorderColor = Color.FromArgb(145, 82, 168, 255);

		private Color bgColor = UiTheme.BgWell;

		private int cornerRadius = 6;

		private bool isFocused = false;

		private bool isHovered = false;

		public TextBox InnerTextBox
		{
			get;
			private set;
		}

		public ModernNumberBox(TextBox innerBox, int radius = 6)
		{
			InnerTextBox = innerBox;
			cornerRadius = radius;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			DoubleBuffered = true;
			BackColor = Color.Transparent;
			base.Padding = Padding.Empty;
			innerBox.BorderStyle = BorderStyle.None;
			innerBox.BackColor = bgColor;
			innerBox.Dock = DockStyle.None;
			EventHandler value = delegate
			{
				isFocused = true;
				Invalidate();
			};
			innerBox.Enter += value;
			innerBox.Leave += delegate
			{
				isFocused = false;
				Invalidate();
			};
			innerBox.MouseEnter += delegate
			{
				isHovered = true;
				Invalidate();
			};
			innerBox.MouseLeave += delegate
			{
				isHovered = false;
				Invalidate();
			};
			base.MouseEnter += delegate
			{
				isHovered = true;
				Invalidate();
			};
			base.MouseLeave += delegate
			{
				isHovered = false;
				Invalidate();
			};
			base.Controls.Add(innerBox);
			CenterInnerBox();
		}

		private void CenterInnerBox()
		{
			if (InnerTextBox == null)
			{
				return;
			}
			int hPad = (base.Width < 52) ? 2 : 5;
			int textH = InnerTextBox.PreferredHeight;
			int top = Math.Max(2, (base.Height - textH) / 2 - 1);
			int innerW = Math.Max(6, base.Width - hPad * 2);
			InnerTextBox.SetBounds(hPad, top, innerW, textH);
		}

		protected override void OnLayout(LayoutEventArgs levent)
		{
			base.OnLayout(levent);
			CenterInnerBox();
		}

		protected override void OnResize(EventArgs eventargs)
		{
			base.OnResize(eventargs);
			CenterInnerBox();
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			if (base.Width <= 2 || base.Height <= 2)
			{
				return;
			}
			RectangleF bounds = new RectangleF(0.5f, 0.5f, (float)base.Width - 2f, (float)base.Height - 2f);
			using (GraphicsPath path = UiTheme.CreateRoundedPath(bounds, cornerRadius))
			{
				using (SolidBrush brush = new SolidBrush(bgColor))
				{
					graphics.FillPath(brush, path);
				}
				if (isFocused)
				{
					RectangleF innerGlow = new RectangleF(1.3f, 1.3f, (float)base.Width - 3.6f, (float)base.Height - 3.6f);
					using (GraphicsPath glowPath = UiTheme.CreateRoundedPath(innerGlow, Math.Max(1f, (float)cornerRadius - 0.8f)))
					using (Pen pen2 = new Pen(Color.FromArgb(48, 82, 168, 255), 1.8f))
					{
						graphics.DrawPath(pen2, glowPath);
					}
				}
				Color color = (isFocused ? focusedBorderColor : (isHovered ? hoverBorderColor : normalBorderColor));
				using (Pen pen = new Pen(color, isFocused ? 1.2f : 1f))
				{
					graphics.DrawPath(pen, path);
				}
			}
		}
	}
	public class DoubleBufferedPanel : Panel
	{
		public DoubleBufferedPanel()
		{
			DoubleBuffered = true;
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			UpdateStyles();
		}
	}
	public class ModernBypassButton : Control
	{
		private bool _active = false;

		private bool isHovered = false;

		private ToolTip tip = new ToolTip();

		public bool Active
		{
			get
			{
				return _active;
			}
			set
			{
				if (_active != value)
				{
					_active = value;
					Invalidate();
					if (this.ActiveChanged != null)
					{
						this.ActiveChanged(this, EventArgs.Empty);
					}
					if (this.CheckedChanged != null)
					{
						this.CheckedChanged(this, EventArgs.Empty);
					}
				}
			}
		}

		public bool Checked
		{
			get
			{
				return Active;
			}
			set
			{
				Active = value;
			}
		}

		public event EventHandler ActiveChanged;

		public event EventHandler CheckedChanged;

		public ModernBypassButton()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			DoubleBuffered = true;
			BackColor = Color.Transparent;
			base.Size = new Size(88, 30);
			Cursor = Cursors.Hand;
			tip.SetToolTip(this, "Bypass all audio processing (Equalizer & ReaComp)");
			EventHandler value = delegate
			{
				isHovered = true;
				Invalidate();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				isHovered = false;
				Invalidate();
			};
			base.Click += delegate
			{
				Active = !Active;
			};
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			RectangleF bounds = new RectangleF(0.5f, 0.5f, (float)base.Width - 2f, (float)base.Height - 2f);
			Color fillColor;
			Color borderColor;
			Color textColor;
			Color dotColor;
			if (_active)
			{
				fillColor = (isHovered ? Color.FromArgb(46, 46, 46) : Color.FromArgb(36, 36, 36));
				borderColor = UiTheme.AccentAlert;
				textColor = UiTheme.TextPrimary;
				dotColor = UiTheme.AccentAlert;
			}
			else
			{
				fillColor = (isHovered ? Color.FromArgb(40, 40, 40) : UiTheme.BgChip);
				borderColor = (isHovered ? UiTheme.AccentBlue : UiTheme.BorderRing);
				textColor = (isHovered ? UiTheme.TextPrimary : UiTheme.TextMuted);
				dotColor = UiTheme.AccentGreen;
			}
			using (GraphicsPath path = UiTheme.CreateRoundedPath(bounds, bounds.Height / 2f))
			{
				using (SolidBrush brush = new SolidBrush(fillColor))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(borderColor, _active ? 1.2f : 1f))
				{
					graphics.DrawPath(pen, path);
				}
			}
			float dotCx = 16f;
			float dotCy = (float)base.Height / 2f;
			float dotR = 3f;
			if (_active)
			{
				using (SolidBrush glowBr = new SolidBrush(Color.FromArgb(42, 237, 237, 237)))
				{
					graphics.FillEllipse(glowBr, dotCx - 5.5f, dotCy - 5.5f, 11f, 11f);
				}
			}
			using (SolidBrush brush3 = new SolidBrush(dotColor))
			{
				graphics.FillEllipse(brush3, dotCx - dotR, dotCy - dotR, dotR * 2f, dotR * 2f);
			}
			using (Font font = UiTheme.MonoFont(8f, FontStyle.Bold))
			using (SolidBrush brush4 = new SolidBrush(textColor))
			using (StringFormat format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
			{
				RectangleF layoutRectangle = new RectangleF(26f, 0f, (float)base.Width - 32f, (float)base.Height);
				graphics.DrawString("BYPASS", font, brush4, layoutRectangle, format);
			}
		}
	}
	public class ModernActionButton : Control
	{
		private bool isHovered = false;

		private bool isPressed = false;

		private ToolTip toolTip = new ToolTip();

		public Color NormalBackColor
		{
			get;
			set;
		}

		public Color HoverBackColor
		{
			get;
			set;
		}

		public Color PressedBackColor
		{
			get;
			set;
		}

		public Color NormalBorderColor
		{
			get;
			set;
		}

		public Color HoverBorderColor
		{
			get;
			set;
		}

		public Color TextColor
		{
			get;
			set;
		}

		public Color HoverTextColor
		{
			get;
			set;
		}

		public int CornerRadius
		{
			get;
			set;
		}

		public string Glyph
		{
			get;
			set;
		}

		public string VectorIcon
		{
			get;
			set;
		}

		public ModernActionButton()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			DoubleBuffered = true;
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			CornerRadius = 6;
			NormalBackColor = UiTheme.BgWell;
			HoverBackColor = UiTheme.BgChip;
			PressedBackColor = UiTheme.BgPressed;
			NormalBorderColor = UiTheme.BorderRing;
			HoverBorderColor = UiTheme.AccentBlue;
			TextColor = UiTheme.TextBody;
			HoverTextColor = UiTheme.TextPrimary;
			Font = UiTheme.DisplayFont(8.8f, FontStyle.Bold);
			EventHandler value = delegate
			{
				isHovered = true;
				Invalidate();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				isHovered = false;
				isPressed = false;
				Invalidate();
			};
			base.MouseDown += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					isPressed = true;
					Invalidate();
				}
			};
			base.MouseUp += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					isPressed = false;
					Invalidate();
				}
			};
		}

		public void SetToolTip(string text)
		{
			toolTip.SetToolTip(this, text);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			if (base.Width <= 2 || base.Height <= 2)
			{
				return;
			}
			RectangleF bounds = new RectangleF(0.5f, 0.5f, (float)base.Width - 2f, (float)base.Height - 2f);
			Color color = ((!base.Enabled) ? Color.FromArgb(14, 14, 14) : (isPressed ? PressedBackColor : (isHovered ? HoverBackColor : NormalBackColor)));
			Color color2 = ((!base.Enabled) ? Color.FromArgb(24, 255, 255, 255) : ((isHovered || isPressed) ? HoverBorderColor : NormalBorderColor));
			Color color3 = ((!base.Enabled) ? Color.FromArgb(80, 80, 80) : (isHovered ? HoverTextColor : TextColor));
			using (GraphicsPath path = UiTheme.CreateRoundedPath(bounds, CornerRadius))
			{
				using (SolidBrush brush = new SolidBrush(color))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(color2, (isHovered || isPressed) ? 1.15f : 1f))
				{
					graphics.DrawPath(pen, path);
				}
			}
			if (VectorIcon == "speaker")
			{
				SizeF textSz = graphics.MeasureString(Text, Font);
				float iconW = 16f;
				float gap = 6f;
				float totalW = iconW + gap + textSz.Width;
				float startX = Math.Max(10f, ((float)base.Width - totalW) / 2f);
				float cy = (float)base.Height / 2f;
				using (SolidBrush brush2 = new SolidBrush(color3))
				{
					graphics.FillRectangle(brush2, startX, cy - 3f, 3f, 6f);
					PointF[] points = new PointF[4]
					{
						new PointF(startX + 3f, cy - 3f),
						new PointF(startX + 7.5f, cy - 6.2f),
						new PointF(startX + 7.5f, cy + 6.2f),
						new PointF(startX + 3f, cy + 3f)
					};
					graphics.FillPolygon(brush2, points);
				}
				using (Pen pen2 = new Pen(color3, 1.35f))
				{
					pen2.StartCap = LineCap.Round;
					pen2.EndCap = LineCap.Round;
					graphics.DrawArc(pen2, startX + 6.5f, cy - 4f, 4.8f, 8f, -35f, 70f);
					graphics.DrawArc(pen2, startX + 7.8f, cy - 6.6f, 7.6f, 13.2f, -40f, 80f);
				}
				using (SolidBrush brush3 = new SolidBrush(color3))
				using (StringFormat format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
				{
					graphics.DrawString(Text, Font, brush3, new RectangleF(startX + iconW + gap - 1f, 0f, (float)base.Width - (startX + iconW + gap), (float)base.Height), format);
				}
			}
			else if (VectorIcon == "bolt")
			{
				SizeF textSz = graphics.MeasureString(Text, Font);
				float iconW = 9f;
				float gap = 4f;
				float totalW = iconW + gap + textSz.Width;
				float startX = Math.Max(7f, ((float)base.Width - totalW) / 2f);
				float cy = (float)base.Height / 2f;
				PointF[] boltPts = new PointF[6]
				{
					new PointF(startX + 5.2f, cy - 5.5f),
					new PointF(startX + 0.8f, cy + 0.6f),
					new PointF(startX + 4.2f, cy + 0.6f),
					new PointF(startX + 3.2f, cy + 5.5f),
					new PointF(startX + 7.8f, cy - 0.6f),
					new PointF(startX + 4.4f, cy - 0.6f)
				};
				using (SolidBrush boltBr = new SolidBrush(color3))
				{
					graphics.FillPolygon(boltBr, boltPts);
				}
				using (SolidBrush brush3 = new SolidBrush(color3))
				using (StringFormat format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
				{
					graphics.DrawString(Text, Font, brush3, new RectangleF(startX + iconW + gap - 1f, 0f, (float)base.Width - (startX + iconW + gap), (float)base.Height), format);
				}
			}
			else
			{
				string s = (string.IsNullOrEmpty(Glyph) ? Text : (Glyph + "  " + Text));
				using (SolidBrush brush3 = new SolidBrush(color3))
				using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
				{
					graphics.DrawString(s, Font, brush3, new RectangleF(0f, 0f, (float)base.Width, (float)base.Height), format);
				}
			}
		}
	}
	public class ModernDropDownList : Control
	{
		public class DropDownItemCollection : IList<string>, ICollection<string>, IEnumerable<string>, IEnumerable
		{
			private List<string> list = new List<string>();

			private ModernDropDownList parent;

			public int Count
			{
				get
				{
					return list.Count;
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return false;
				}
			}

			public string this[int index]
			{
				get
				{
					return list[index];
				}
				set
				{
					list[index] = value;
					parent.Invalidate();
				}
			}

			public DropDownItemCollection(ModernDropDownList p)
			{
				parent = p;
			}

			public void Add(string item)
			{
				list.Add(item);
				parent.Invalidate();
			}

			public void Clear()
			{
				list.Clear();
				parent.SelectedIndex = -1;
				parent.Invalidate();
			}

			public bool Contains(string item)
			{
				return list.Contains(item);
			}

			public void CopyTo(string[] array, int arrayIndex)
			{
				list.CopyTo(array, arrayIndex);
			}

			public int IndexOf(string item)
			{
				return list.IndexOf(item);
			}

			public void Insert(int index, string item)
			{
				list.Insert(index, item);
				parent.Invalidate();
			}

			public bool Remove(string item)
			{
				bool result = list.Remove(item);
				parent.Invalidate();
				return result;
			}

			public void RemoveAt(int index)
			{
				list.RemoveAt(index);
				parent.Invalidate();
			}

			public IEnumerator<string> GetEnumerator()
			{
				return list.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return list.GetEnumerator();
			}
		}

		private bool isHovered = false;

		private bool isPressed = false;

		private DropDownItemCollection _items;

		private int selectedIndex = -1;

		private ContextMenuStrip dropMenu;

		public DropDownItemCollection Items
		{
			get
			{
				return _items;
			}
		}

		public int SelectedIndex
		{
			get
			{
				return selectedIndex;
			}
			set
			{
				if (value >= -1 && value < _items.Count && selectedIndex != value)
				{
					selectedIndex = value;
					Invalidate();
					if (this.SelectedIndexChanged != null)
					{
						this.SelectedIndexChanged(this, EventArgs.Empty);
					}
				}
			}
		}

		public object SelectedItem
		{
			get
			{
				return (selectedIndex >= 0 && selectedIndex < _items.Count) ? _items[selectedIndex] : null;
			}
			set
			{
				if (value == null)
				{
					SelectedIndex = -1;
					return;
				}
				int num = _items.IndexOf(value.ToString());
				if (num >= 0)
				{
					SelectedIndex = num;
				}
			}
		}

		public event EventHandler SelectedIndexChanged;

		public ModernDropDownList()
		{
			_items = new DropDownItemCollection(this);
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			DoubleBuffered = true;
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			base.Size = new Size(220, 30);
			Font = UiTheme.DisplayFont(9.2f, FontStyle.Bold);
			EventHandler value = delegate
			{
				isHovered = true;
				Invalidate();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				isHovered = false;
				isPressed = false;
				Invalidate();
			};
			base.MouseDown += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					isPressed = true;
					Invalidate();
				}
			};
			base.MouseUp += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					isPressed = false;
					Invalidate();
				}
			};
			base.Click += delegate
			{
				ShowDropdown();
			};
		}

		public void ShowDropdown()
		{
			dropMenu = new ContextMenuStrip();
			dropMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
			dropMenu.ShowImageMargin = false;
			dropMenu.Font = UiTheme.DisplayFont(9f, FontStyle.Bold);
			dropMenu.MinimumSize = new Size(base.Width, 0);
			for (int i = 0; i < _items.Count; i++)
			{
				int idx = i;
				ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("  " + _items[i]);
				toolStripMenuItem.ForeColor = ((idx == selectedIndex) ? UiTheme.AccentBlue : UiTheme.TextBody);
				toolStripMenuItem.Click += delegate
				{
					SelectedIndex = idx;
				};
				dropMenu.Items.Add(toolStripMenuItem);
			}
			dropMenu.Show(this, new Point(0, base.Height + 2));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			RectangleF bounds = new RectangleF(0.5f, 0.5f, (float)base.Width - 2f, (float)base.Height - 2f);
			Color color = (isPressed ? UiTheme.BgPressed : (isHovered ? UiTheme.BgChip : UiTheme.BgWell));
			Color color2 = ((isHovered || isPressed) ? UiTheme.AccentBlue : UiTheme.BorderRing);
			using (GraphicsPath path = UiTheme.CreateRoundedPath(bounds, 6f))
			{
				using (SolidBrush brush = new SolidBrush(color))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(color2, (isHovered || isPressed) ? 1.15f : 1f))
				{
					graphics.DrawPath(pen, path);
				}
			}
			string s = ((SelectedItem != null) ? SelectedItem.ToString() : "Select Preset...");
			using (SolidBrush brush2 = new SolidBrush((SelectedItem != null) ? UiTheme.TextPrimary : UiTheme.TextMuted))
			using (StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
			{
				RectangleF layoutRectangle = new RectangleF(12f, 0f, (float)base.Width - 36f, (float)base.Height);
				graphics.DrawString(s, Font, brush2, layoutRectangle, format);
			}
			int num = base.Width - 16;
			int num2 = base.Height / 2;
			Color color3 = ((isHovered || isPressed) ? UiTheme.AccentBlue : UiTheme.TextMuted);
			using (Pen pen2 = new Pen(color3, 1.6f))
			{
				pen2.StartCap = LineCap.Round;
				pen2.EndCap = LineCap.Round;
				graphics.DrawLine(pen2, num - 4, num2 - 2, num, num2 + 2);
				graphics.DrawLine(pen2, num, num2 + 2, num + 4, num2 - 2);
			}
		}
	}
	public class FilterTypeButton : Control
	{
		private string filterType = "PK";

		private bool isHovered = false;

		private ToolTip tip = new ToolTip();

		private ContextMenuStrip typeMenu;

		public string FilterType
		{
			get
			{
				return filterType;
			}
			set
			{
				string b = BiquadFilter.NormalizeType(value);
				if (filterType != b)
				{
					filterType = b;
					UpdateToolTip();
					Invalidate();
				}
			}
		}

		public event EventHandler FilterTypeChanged;

		public FilterTypeButton(string initialType = "PK")
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			DoubleBuffered = true;
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			base.Size = new Size(36, 20);
			FilterType = initialType;
			InitMenu();
			EventHandler value = delegate
			{
				isHovered = true;
				Invalidate();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				isHovered = false;
				Invalidate();
			};
			base.MouseClick += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					CycleType();
				}
				else if (e.Button == MouseButtons.Right)
				{
					typeMenu.Show(this, new Point(0, base.Height));
				}
			};
		}

		private void InitMenu()
		{
			typeMenu = new ContextMenuStrip();
			typeMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
			ToolStripMenuItem miPk = new ToolStripMenuItem("  Peak (PK)") { ForeColor = UiTheme.TextBody };
			miPk.Click += delegate
			{
				SelectType("PK");
			};
			ToolStripMenuItem miLs = new ToolStripMenuItem("  Low-Shelf (LS)") { ForeColor = UiTheme.TextBody };
			miLs.Click += delegate
			{
				SelectType("LS");
			};
			ToolStripMenuItem miHs = new ToolStripMenuItem("  High-Shelf (HS)") { ForeColor = UiTheme.TextBody };
			miHs.Click += delegate
			{
				SelectType("HS");
			};
			ToolStripMenuItem miHp = new ToolStripMenuItem("  High-Pass (HP)") { ForeColor = UiTheme.TextBody };
			miHp.Click += delegate
			{
				SelectType("HP");
			};
			typeMenu.Items.AddRange(new ToolStripItem[4]
			{
				miPk,
				miLs,
				miHs,
				miHp
			});
			typeMenu.Opening += delegate
			{
				miPk.Checked = filterType == "PK";
				miLs.Checked = filterType == "LS";
				miHs.Checked = filterType == "HS";
				miHp.Checked = filterType == "HP";
			};
		}

		private void SelectType(string t)
		{
			string b = BiquadFilter.NormalizeType(t);
			if (filterType != b)
			{
				filterType = b;
				UpdateToolTip();
				Invalidate();
				if (this.FilterTypeChanged != null)
				{
					this.FilterTypeChanged(this, EventArgs.Empty);
				}
			}
		}

		private void CycleType()
		{
			string t;
			switch (filterType)
			{
			case "PK":
				t = "LS";
				break;
			case "LS":
				t = "HS";
				break;
			case "HS":
				t = "HP";
				break;
			default:
				t = "PK";
				break;
			}
			SelectType(t);
		}

		private void UpdateToolTip()
		{
			string caption;
			switch (filterType)
			{
			case "LS":
				caption = "Low-Shelf Filter (Click to cycle / Right-click)";
				break;
			case "HS":
				caption = "High-Shelf Filter (Click to cycle / Right-click)";
				break;
			case "HP":
				caption = "High-Pass Filter (Click to cycle / Right-click)";
				break;
			default:
				caption = "Peak Filter (Click to cycle / Right-click)";
				break;
			}
			tip.SetToolTip(this, caption);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			Color color = (isHovered ? UiTheme.BgChip : UiTheme.BgWell);
			Color color2 = (isHovered ? UiTheme.AccentBlue : UiTheme.BorderRing);
			RectangleF bounds = new RectangleF(0.5f, 0.5f, (float)base.Width - 2f, (float)base.Height - 2f);
			using (GraphicsPath path = UiTheme.CreateRoundedPath(bounds, 5f))
			{
				using (SolidBrush brush = new SolidBrush(color))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(color2, 1f))
				{
					graphics.DrawPath(pen, path);
				}
			}
			Color color3 = (isHovered ? UiTheme.AccentBlue : UiTheme.TextMuted);
			float num = (float)base.Width / 2f;
			float num2 = (float)base.Height / 2f;
			float num3 = Math.Min((float)base.Width - 10f, 20f);
			float num4 = 8.5f;
			float num5 = num - num3 / 2f;
			float num6 = num + num3 / 2f;
			float num7 = num2;
			using (Pen pen2 = new Pen(color3, 1.6f))
			{
				pen2.StartCap = LineCap.Round;
				pen2.EndCap = LineCap.Round;
				pen2.LineJoin = LineJoin.Round;
				switch (filterType)
				{
				case "PK":
				{
					PointF[] points = new PointF[7]
					{
						new PointF(num5, num7 + 2f),
						new PointF(num5 + num3 * 0.22f, num7 + 2f),
						new PointF(num - num3 * 0.12f, num7 - num4 * 0.44f),
						new PointF(num, num7 - num4 * 0.44f),
						new PointF(num + num3 * 0.12f, num7 - num4 * 0.44f),
						new PointF(num6 - num3 * 0.22f, num7 + 2f),
						new PointF(num6, num7 + 2f)
					};
					graphics.DrawCurve(pen2, points, 0.5f);
					break;
				}
				case "LS":
				{
					PointF[] points = new PointF[5]
					{
						new PointF(num5, num7 - num4 * 0.4f),
						new PointF(num5 + num3 * 0.22f, num7 - num4 * 0.4f),
						new PointF(num, num7),
						new PointF(num6 - num3 * 0.22f, num7 + num4 * 0.4f),
						new PointF(num6, num7 + num4 * 0.4f)
					};
					graphics.DrawCurve(pen2, points, 0.5f);
					break;
				}
				case "HS":
				{
					PointF[] points = new PointF[5]
					{
						new PointF(num5, num7 + num4 * 0.4f),
						new PointF(num5 + num3 * 0.22f, num7 + num4 * 0.4f),
						new PointF(num, num7),
						new PointF(num6 - num3 * 0.22f, num7 - num4 * 0.4f),
						new PointF(num6, num7 - num4 * 0.4f)
					};
					graphics.DrawCurve(pen2, points, 0.5f);
					break;
				}
				case "HP":
				{
					PointF[] points = new PointF[5]
					{
						new PointF(num5, num7 + num4 * 0.45f),
						new PointF(num5 + num3 * 0.15f, num7 + num4 * 0.38f),
						new PointF(num5 + num3 * 0.36f, num7 - num4 * 0.34f),
						new PointF(num5 + num3 * 0.55f, num7 - num4 * 0.4f),
						new PointF(num6, num7 - num4 * 0.4f)
					};
					graphics.DrawCurve(pen2, points, 0.45f);
					break;
				}
				}
			}
		}
	}
	public class WindowControlButton : Control
	{
		public enum ButtonType
		{
			Close,
			Maximize,
			Minimize
		}

		private bool isHovered = false;

		private static readonly Color CloseColor = UiTheme.AccentAlert;

		private static readonly Color MaxColor = UiTheme.AccentBlue;

		private static readonly Color MinColor = UiTheme.AccentGreen;

		private static readonly Color IdleColor = Color.FromArgb(42, 42, 42);

		public ButtonType Type
		{
			get;
			set;
		}

		public WindowControlButton(ButtonType type)
		{
			Type = type;
			base.Size = new Size(14, 14);
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			isHovered = true;
			Invalidate();
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			isHovered = false;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			Color color = IdleColor;
			if (isHovered)
			{
				color = ((Type == ButtonType.Close) ? CloseColor : ((Type == ButtonType.Maximize) ? MaxColor : MinColor));
			}
			RectangleF circle = new RectangleF(0.5f, 0.5f, (float)base.Width - 2f, (float)base.Height - 2f);
			using (SolidBrush brush = new SolidBrush(color))
			{
				graphics.FillEllipse(brush, circle);
			}
			if (!isHovered)
			{
				using (Pen ring = new Pen(UiTheme.BorderRing, 1f))
				{
					graphics.DrawEllipse(ring, circle);
				}
				return;
			}
			using (Pen pen = new Pen(UiTheme.CtaText, 1.4f))
			{
				pen.StartCap = LineCap.Round;
				pen.EndCap = LineCap.Round;
				float num = ((float)base.Width - 1f) / 2f;
				float num2 = ((float)base.Height - 1f) / 2f;
				float num3 = 2.6f;
				if (Type == ButtonType.Close)
				{
					graphics.DrawLine(pen, num - num3, num2 - num3, num + num3, num2 + num3);
					graphics.DrawLine(pen, num + num3, num2 - num3, num - num3, num2 + num3);
				}
				else if (Type == ButtonType.Minimize)
				{
					graphics.DrawLine(pen, num - num3, num2, num + num3, num2);
				}
				else if (Type == ButtonType.Maximize)
				{
					graphics.DrawRectangle(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
				}
			}
		}
	}
	public class GearIconButton : Control
	{
		private bool isHovered = false;

		private bool isPressed = false;

		private ToolTip toolTip = new ToolTip();

		public GearIconButton()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			DoubleBuffered = true;
			BackColor = Color.Transparent;
			base.Size = new Size(30, 30);
			Cursor = Cursors.Hand;
			toolTip.SetToolTip(this, "Settings");
			EventHandler value = delegate
			{
				isHovered = true;
				Invalidate();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				isHovered = false;
				isPressed = false;
				Invalidate();
			};
			base.MouseDown += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					isPressed = true;
					Invalidate();
				}
			};
			base.MouseUp += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					isPressed = false;
					Invalidate();
				}
			};
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			Color color = (isPressed ? UiTheme.BgPressed : (isHovered ? UiTheme.BgChip : UiTheme.BgWell));
			Color color2 = (isHovered ? UiTheme.AccentBlue : UiTheme.BorderRing);
			RectangleF bounds = new RectangleF(0.5f, 0.5f, (float)base.Width - 2f, (float)base.Height - 2f);
			using (GraphicsPath path = UiTheme.CreateRoundedPath(bounds, 6f))
			{
				using (SolidBrush brush = new SolidBrush(color))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(color2, 1f))
				{
					graphics.DrawPath(pen, path);
				}
			}
			Color color3 = (isHovered ? UiTheme.AccentBlue : UiTheme.TextMuted);
			float num = ((float)base.Width - 1f) / 2f;
			float num2 = ((float)base.Height - 1f) / 2f;
			float num3 = 7.8f;
			float num4 = 5.6f;
			float num5 = 2.8f;
			int num6 = 8;
			using (GraphicsPath graphicsPath = new GraphicsPath())
			{
				List<PointF> list = new List<PointF>();
				for (int i = 0; i < num6; i++)
				{
					double num7 = (double)i * (Math.PI * 2.0 / (double)num6);
					double num8 = Math.PI * 2.0 / (double)num6 / 4.0;
					double num9 = num7 - num8 * 0.8;
					double num10 = num7 - num8 * 0.4;
					double num11 = num7 + num8 * 0.4;
					double num12 = num7 + num8 * 0.8;
					list.Add(new PointF(num + (float)((double)num4 * Math.Cos(num9)), num2 + (float)((double)num4 * Math.Sin(num9))));
					list.Add(new PointF(num + (float)((double)num3 * Math.Cos(num10)), num2 + (float)((double)num3 * Math.Sin(num10))));
					list.Add(new PointF(num + (float)((double)num3 * Math.Cos(num11)), num2 + (float)((double)num3 * Math.Sin(num11))));
					list.Add(new PointF(num + (float)((double)num4 * Math.Cos(num12)), num2 + (float)((double)num4 * Math.Sin(num12))));
				}
				graphicsPath.AddPolygon(list.ToArray());
				graphicsPath.AddEllipse(num - num5, num2 - num5, num5 * 2f, num5 * 2f);
				using (SolidBrush brush2 = new SolidBrush(color3))
				{
					graphics.FillPath(brush2, graphicsPath);
				}
			}
		}
	}
	[ComImport]
	[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
	internal class MMDeviceEnumeratorComObject
	{
	}
	internal enum EDataFlow
	{
		eRender,
		eCapture,
		eAll
	}
	internal enum ERole
	{
		eConsole,
		eMultimedia,
		eCommunications
	}
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IMMDeviceEnumerator
	{
		int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IntPtr ppDevices);

		int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);

		int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);

		int RegisterEndpointNotificationCallback(IntPtr pClient);

		int UnregisterEndpointNotificationCallback(IntPtr pClient);
	}
	[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IMMDevice
	{
		int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

		int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);

		int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

		int GetState(out int pdwState);
	}
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
	internal interface IAudioMeterInformation
	{
		int GetPeakValue(out float pfPeak);

		int GetMeteringChannelCount(out int pnChannelCount);

		int GetChannelsPeakValues(int u32ChannelCount, IntPtr afPeakValues);

		int QueryHardwareSupport(out int pdwHardwareSupportMask);
	}
	public class ModernVuMeterControl : Control
	{
		private float curL = 0f;
		private float curR = 0f;
		private float peakL = 0f;
		private float peakR = 0f;
		private int peakHoldL = 0;
		private int peakHoldR = 0;
		private int clipHold = 0;
		private float dispDbL = -60f;
		private float dispDbR = -60f;

		// Ableton-style Maximum Peak Hold & Reset
		private float maxPeakDbL = -60f;
		private float maxPeakDbR = -60f;
		private bool isClippedL = false;
		private bool isClippedR = false;

		// Interactive hit test rectangles
		private Rectangle rectPeakBoxL = Rectangle.Empty;
		private Rectangle rectPeakBoxR = Rectangle.Empty;
		private Rectangle rectResetBtn = Rectangle.Empty;
		private Rectangle rectBottomArea = Rectangle.Empty;

		// Hover states
		private bool isHoverPeakL = false;
		private bool isHoverPeakR = false;
		private bool isHoverReset = false;
		private ToolTip meterToolTip = null;

		public ModernVuMeterControl()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
			BackColor = Color.Transparent;
			Dock = DockStyle.Fill;
			try
			{
				meterToolTip = new ToolTip();
				meterToolTip.AutoPopDelay = 5000;
				meterToolTip.InitialDelay = 350;
				meterToolTip.ReshowDelay = 150;
				meterToolTip.SetToolTip(this, "Ableton-style Peak Hold: Click peak box or RESET to clear peak");
			}
			catch { }
		}

		public void ResetPeak()
		{
			maxPeakDbL = -60f;
			maxPeakDbR = -60f;
			isClippedL = false;
			isClippedR = false;
			peakL = curL;
			peakR = curR;
			peakHoldL = 0;
			peakHoldR = 0;
			clipHold = 0;
			Invalidate();
		}

		public void UpdateLevels(float rawL, float rawR)
		{
			float dbL = ((rawL > 0.0001f) ? ((float)(20.0 * Math.Log10(rawL))) : (-60f));
			float dbR = ((rawR > 0.0001f) ? ((float)(20.0 * Math.Log10(rawR))) : (-60f));
			dispDbL = dbL;
			dispDbR = dbR;

			// Ableton-style maximum peak tracking
			if (dbL > maxPeakDbL)
			{
				maxPeakDbL = dbL;
				if (dbL >= -0.05f || rawL >= 0.999f)
				{
					isClippedL = true;
				}
			}
			if (dbR > maxPeakDbR)
			{
				maxPeakDbR = dbR;
				if (dbR >= -0.05f || rawR >= 0.999f)
				{
					isClippedR = true;
				}
			}

			float num3 = DbToMeterFraction(dbL);
			float num4 = DbToMeterFraction(dbR);
			curL = ((num3 > curL) ? num3 : Math.Max(0f, curL - 0.035f));
			curR = ((num4 > curR) ? num4 : Math.Max(0f, curR - 0.035f));
			if (num3 >= peakL)
			{
				peakL = num3;
				peakHoldL = 30;
			}
			else if (peakHoldL > 0)
			{
				peakHoldL--;
			}
			else
			{
				peakL = Math.Max(curL, peakL - 0.015f);
			}
			if (num4 >= peakR)
			{
				peakR = num4;
				peakHoldR = 30;
			}
			else if (peakHoldR > 0)
			{
				peakHoldR--;
			}
			else
			{
				peakR = Math.Max(curR, peakR - 0.015f);
			}
			if (dbL >= -0.05f || dbR >= -0.05f || rawL >= 0.999f || rawR >= 0.999f)
			{
				clipHold = 35;
			}
			else if (clipHold > 0)
			{
				clipHold--;
			}
			Invalidate();
		}

		private static float DbToMeterFraction(float db)
		{
			if (db <= -60f) return 0f;
			if (db >= 3f) return 1f;
			if (db < -36f) return 0.2f * (db - -60f) / 24f;
			if (db < -24f) return 0.2f + 0.2f * (db - -36f) / 12f;
			if (db < -18f) return 0.4f + 0.15f * (db - -24f) / 6f;
			if (db < -12f) return 0.55f + 0.15f * (db - -18f) / 6f;
			if (db < -6f) return 0.7f + 0.15f * (db - -12f) / 6f;
			if (db < 0f) return 0.85f + 0.09f * (db - -6f) / 6f;
			return 0.94f + 0.06f * (db - 0f) / 3f;
		}

		private static string FormatPeakDb(float db)
		{
			if (db <= -59.5f) return "-INF";
			if (db >= 0.05f) return string.Format(CultureInfo.InvariantCulture, "+{0:0.0}", db);
			return string.Format(CultureInfo.InvariantCulture, "{0:0.0}", db);
		}

		private static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			int num = radius * 2;
			if (num > bounds.Width)
			{
				num = bounds.Width;
			}
			if (num > bounds.Height)
			{
				num = bounds.Height;
			}
			graphicsPath.AddArc(bounds.X, bounds.Y, num, num, 180f, 90f);
			graphicsPath.AddArc(bounds.Right - num, bounds.Y, num, num, 270f, 90f);
			graphicsPath.AddArc(bounds.Right - num, bounds.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(bounds.X, bounds.Bottom - num, num, num, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

			int margin = 6;
			int peakBoxW = 48;
			int peakBoxH = 16;
			int meterBarH = 16;
			int gap = 6;

			int labelW = 14;
			int barX = margin + labelW;
			int barW = base.Width - barX - peakBoxW - margin - gap;
			if (barW < 30) return;

			int peakBoxX = barX + barW + gap;
			rectPeakBoxL = new Rectangle(peakBoxX, 4, peakBoxW, peakBoxH);
			rectPeakBoxR = new Rectangle(peakBoxX, 24, peakBoxW, peakBoxH);
			rectResetBtn = new Rectangle(peakBoxX, 64, peakBoxW, 18);
			rectBottomArea = new Rectangle(margin, 64, barW + labelW, 18);

			// Draw channel bars (L and R)
			DrawChannelMeter(graphics, "L", margin, 4, labelW, barX, barW, meterBarH, curL, peakL);
			DrawChannelMeter(graphics, "R", margin, 24, labelW, barX, barW, meterBarH, curR, peakR);

			// Draw Ableton-style Peak Readout Boxes
			DrawAbletonPeakBox(graphics, rectPeakBoxL, maxPeakDbL, isClippedL, isHoverPeakL);
			DrawAbletonPeakBox(graphics, rectPeakBoxR, maxPeakDbR, isClippedR, isHoverPeakR);

			// Draw dB scale ticks and labels under the bars without overlap
			int tickY = 44;
			string[] labels = new string[8] { "-60", "-36", "-24", "-18", "-12", "-6", "0", "+3" };
			float[] fracs = new float[8] { 0f, 0.2f, 0.4f, 0.55f, 0.7f, 0.85f, 0.94f, 1f };

			using (Pen pen = new Pen(Color.FromArgb(46, 46, 46), 1f))
			using (Font font = UiTheme.MonoFont(6.1f))
			using (SolidBrush brush = new SolidBrush(UiTheme.TextMuted))
			{
				float lastLabelRight = -1000f;
				for (int i = 0; i < labels.Length; i++)
				{
					int tickX = barX + (int)Math.Round(fracs[i] * (float)barW);
					graphics.DrawLine(pen, tickX, tickY, tickX, tickY + 3);
					float w = graphics.MeasureString(labels[i], font).Width;
					float drawX;
					if (i == 0)
					{
						drawX = (float)barX - 2f;
					}
					else if (i == labels.Length - 1)
					{
						drawX = (float)(barX + barW) - w + 4f;
					}
					else
					{
						drawX = (float)tickX - w / 2f;
					}
					if (drawX >= lastLabelRight + 2f)
					{
						graphics.DrawString(labels[i], font, brush, drawX, tickY + 4);
						lastLabelRight = drawX + w - 2f;
					}
				}
			}

			// Draw Bottom Row: Status / Max Peak Readout + Reset Pill
			int bottomY = 64;
			using (Font fontBottom = UiTheme.MonoFont(7.1f, FontStyle.Bold))
			using (StringFormat sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
			{
				bool hasClip = isClippedL || isClippedR;
				Color textColor = hasClip ? UiTheme.AccentAlert : UiTheme.TextMuted;
				string maxPeakSummary = string.Format("PEAK  L {0} | R {1}", FormatPeakDb(maxPeakDbL), FormatPeakDb(maxPeakDbR));

				if (hasClip)
				{
					RectangleF clipBadgeRect = new RectangleF((float)margin + 0.5f, (float)bottomY + 1.5f, 34f, 15f);
					using (GraphicsPath path = UiTheme.CreateRoundedPath(clipBadgeRect, clipBadgeRect.Height / 2f))
					using (SolidBrush brushClip = new SolidBrush(UiTheme.AccentAlert))
					{
						graphics.FillPath(brushClip, path);
					}
					using (Font fontClip = UiTheme.MonoFont(6.3f, FontStyle.Bold))
					using (SolidBrush clipTxtBr = new SolidBrush(UiTheme.CtaText))
					using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
					{
						graphics.DrawString("CLIP", fontClip, clipTxtBr, clipBadgeRect, sf);
					}
					using (SolidBrush brSum = new SolidBrush(textColor))
					{
						graphics.DrawString(maxPeakSummary, fontBottom, brSum, new RectangleF(margin + 39, bottomY, barW + labelW - 39, 18), sfLeft);
					}
				}
				else
				{
					using (SolidBrush brSum = new SolidBrush(textColor))
					{
						graphics.DrawString(maxPeakSummary, fontBottom, brSum, new RectangleF(margin, bottomY, barW + labelW, 18), sfLeft);
					}
				}
			}

			// Draw Reset Button
			DrawResetButton(graphics, rectResetBtn, isHoverReset);
		}

		private void DrawAbletonPeakBox(Graphics g, Rectangle rect, float peakDb, bool isClipped, bool isHover)
		{
			bool clip = isClipped || peakDb >= 0.0f;
			Color bgColor = clip ? UiTheme.AccentAlert : (isHover ? UiTheme.BgChip : UiTheme.BgWell);
			Color borderColor = clip ? UiTheme.AccentAlert : (isHover ? UiTheme.AccentBlue : UiTheme.BorderRing);
			Color textColor = clip ? UiTheme.CtaText : ((peakDb > -59.5f) ? UiTheme.AccentBlue : UiTheme.TextMuted);

			RectangleF rf = new RectangleF((float)rect.X + 0.5f, (float)rect.Y + 0.5f, (float)rect.Width - 1f, (float)rect.Height - 1f);
			using (GraphicsPath path = UiTheme.CreateRoundedPath(rf, 4f))
			using (SolidBrush brush = new SolidBrush(bgColor))
			using (Pen pen = new Pen(borderColor, 1f))
			{
				g.FillPath(brush, path);
				g.DrawPath(pen, path);
			}

			string text = FormatPeakDb(peakDb);
			using (Font font = UiTheme.MonoFont(6.8f, FontStyle.Bold))
			using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
			using (SolidBrush brushText = new SolidBrush(textColor))
			{
				g.DrawString(text, font, brushText, rf, sf);
			}
		}

		private void DrawResetButton(Graphics g, Rectangle rect, bool isHover)
		{
			Color bgColor = isHover ? UiTheme.BgPressed : UiTheme.BgChip;
			Color borderColor = isHover ? UiTheme.AccentBlue : UiTheme.BorderRing;
			Color textColor = isHover ? UiTheme.TextPrimary : UiTheme.TextBody;

			RectangleF rf = new RectangleF((float)rect.X + 0.5f, (float)rect.Y + 0.5f, (float)rect.Width - 1f, (float)rect.Height - 1f);
			using (GraphicsPath path = UiTheme.CreateRoundedPath(rf, rf.Height / 2f))
			using (SolidBrush brush = new SolidBrush(bgColor))
			using (Pen pen = new Pen(borderColor, 1f))
			{
				g.FillPath(brush, path);
				g.DrawPath(pen, path);
			}

			using (Font font = UiTheme.MonoFont(6.8f, FontStyle.Bold))
			using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
			using (SolidBrush brushText = new SolidBrush(textColor))
			{
				g.DrawString("RST", font, brushText, rf, sf);
			}
		}

		private void DrawChannelMeter(Graphics g, string label, int labelX, int y, int labelW, int barX, int barW, int barH, float val, float peak)
		{
			using (Font font = UiTheme.MonoFont(7.8f, FontStyle.Bold))
			using (SolidBrush brLbl = new SolidBrush((val > 0.01f) ? UiTheme.AccentBlue : UiTheme.TextMuted))
			using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
			{
				g.DrawString(label, font, brLbl, new RectangleF(labelX, y, labelW, barH), sf);
			}

			RectangleF rectF = new RectangleF((float)barX + 0.5f, (float)y + 0.5f, (float)barW - 1f, (float)barH - 1f);
			using (GraphicsPath path = UiTheme.CreateRoundedPath(rectF, 4f))
			{
				using (SolidBrush brush = new SolidBrush(UiTheme.BgWell))
				{
					g.FillPath(brush, path);
				}
				using (Pen pen = new Pen(UiTheme.BorderRing, 1f))
				{
					g.DrawPath(pen, path);
				}
			}

			float fillW = Math.Max(0f, Math.Min(1f, val)) * (rectF.Width - 2f);
			if (fillW > 2f)
			{
				RectangleF fillRect = new RectangleF(rectF.X + 1f, rectF.Y + 1f, fillW, rectF.Height - 2f);
				using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(rectF, UiTheme.AccentBlue, UiTheme.AccentAlert, LinearGradientMode.Horizontal))
				{
					ColorBlend colorBlend = new ColorBlend();
					colorBlend.Colors = new Color[3]
					{
						UiTheme.AccentBlue,
						UiTheme.AccentGreen,
						UiTheme.AccentAlert
					};
					colorBlend.Positions = new float[3]
					{
						0f,
						0.78f,
						1f
					};
					linearGradientBrush.InterpolationColors = colorBlend;
					using (GraphicsPath path2 = UiTheme.CreateRoundedPath(fillRect, 3f))
					{
						g.FillPath(linearGradientBrush, path2);
					}
				}
			}

			float peakX = rectF.X + 1f + Math.Max(0f, Math.Min(1f, peak)) * (rectF.Width - 3f);
			using (Pen pen2 = new Pen(UiTheme.TextPrimary, 1.5f))
			{
				g.DrawLine(pen2, peakX, rectF.Y + 1.5f, peakX, rectF.Bottom - 1.5f);
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			bool hoverL = rectPeakBoxL.Contains(e.Location);
			bool hoverR = rectPeakBoxR.Contains(e.Location);
			bool hoverReset = rectResetBtn.Contains(e.Location);
			bool hoverBottom = rectBottomArea.Contains(e.Location);

			if (hoverL || hoverR || hoverReset || hoverBottom)
			{
				Cursor = Cursors.Hand;
			}
			else
			{
				Cursor = Cursors.Default;
			}

			if (hoverL != isHoverPeakL || hoverR != isHoverPeakR || hoverReset != isHoverReset)
			{
				isHoverPeakL = hoverL;
				isHoverPeakR = hoverR;
				isHoverReset = hoverReset;
				Invalidate();
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			Cursor = Cursors.Default;
			if (isHoverPeakL || isHoverPeakR || isHoverReset)
			{
				isHoverPeakL = false;
				isHoverPeakR = false;
				isHoverReset = false;
				Invalidate();
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button == MouseButtons.Left)
			{
				if (rectPeakBoxL.Contains(e.Location) ||
					rectPeakBoxR.Contains(e.Location) ||
					rectResetBtn.Contains(e.Location) ||
					rectBottomArea.Contains(e.Location))
				{
					ResetPeak();
				}
			}
		}

		protected override void OnDoubleClick(EventArgs e)
		{
			base.OnDoubleClick(e);
			ResetPeak();
		}
	}
	public class SettingsDialog : Form
	{
		private MainForm main;
		private CheckBox chkStartWithWindows;
		private CheckBox chkAutoSwitchPreset;
		private Panel pnlDeviceMappings;

		private Color BG_MAIN = UiTheme.BgCanvas;
		private Color BG_CARD = UiTheme.BgCard;
		private Color BORDER_CARD = UiTheme.BorderRing;
		private Color ACCENT_CYAN = UiTheme.AccentBlue;
		private Color TEXT_MUTED = UiTheme.TextMuted;

		public SettingsDialog(MainForm parent = null)
		{
			main = parent;
			Text = "Settings - DolphinXvdo v2.3";
			base.Size = new Size(580, 560);
			base.StartPosition = FormStartPosition.CenterParent;
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			BackColor = BG_MAIN;
			ForeColor = UiTheme.TextBody;
			Font = UiTheme.DisplayFont(9f, FontStyle.Regular);

			try
			{
				if (main != null && main.Icon != null)
				{
					base.Icon = main.Icon;
				}
			}
			catch { }

			InitializeUI();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			MainForm.ApplyDiscordDarkStyle(base.Handle);
		}

		private void InitializeUI()
		{
			// Header
			Panel pnlHeader = new Panel
			{
				Dock = DockStyle.Top,
				Height = 58,
				BackColor = BG_CARD,
				Padding = new Padding(18, 10, 18, 8)
			};
			pnlHeader.Paint += delegate(object s, PaintEventArgs pe)
			{
				using (Pen pen = new Pen(UiTheme.BorderSubtle, 1f))
				{
					pe.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
				}
			};
			Label lblTitle = new Label
			{
				Text = "Settings & Device Automation",
				Font = UiTheme.DisplayFont(11.5f, FontStyle.Bold),
				ForeColor = UiTheme.TextPrimary,
				AutoSize = true,
				Location = new Point(16, 8)
			};
			Label lblSub = new Label
			{
				Text = "Configure Windows startup and automatic preset switching per audio output device.",
				Font = UiTheme.DisplayFont(8.2f),
				ForeColor = TEXT_MUTED,
				AutoSize = true,
				Location = new Point(17, 32)
			};
			pnlHeader.Controls.Add(lblTitle);
			pnlHeader.Controls.Add(lblSub);
			base.Controls.Add(pnlHeader);

			// Bottom Panel
			Panel pnlBottom = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 54,
				BackColor = BG_CARD,
				Padding = new Padding(18, 10, 18, 10)
			};
			pnlBottom.Paint += delegate(object s, PaintEventArgs pe)
			{
				using (Pen pen = new Pen(UiTheme.BorderSubtle, 1f))
				{
					pe.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
				}
			};
			ModernActionButton btnClose = new ModernActionButton
			{
				Text = "Save & Close",
				Font = UiTheme.DisplayFont(9f, FontStyle.Bold),
				CornerRadius = 6,
				NormalBackColor = UiTheme.CtaBg,
				HoverBackColor = UiTheme.CtaHoverBg,
				PressedBackColor = UiTheme.CtaPressedBg,
				NormalBorderColor = UiTheme.CtaBg,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.CtaText,
				HoverTextColor = UiTheme.CtaText,
				Size = new Size(118, 32),
				Anchor = (AnchorStyles.Top | AnchorStyles.Right),
				Location = new Point(pnlBottom.Width - 136, 11),
				Cursor = Cursors.Hand
			};
			btnClose.Click += delegate
			{
				if (main != null)
				{
					if (chkAutoSwitchPreset != null)
					{
						main.AutoSwitchPresetByDevice = chkAutoSwitchPreset.Checked;
					}
					main.SaveDevicePresetSettings();
				}
				base.DialogResult = DialogResult.OK;
				Close();
			};
			pnlBottom.Controls.Add(btnClose);
			base.Controls.Add(pnlBottom);

			// Content Scroll Panel
			Panel pnlContent = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = BG_MAIN,
				Padding = new Padding(18, 12, 18, 12)
			};
			base.Controls.Add(pnlContent);
			pnlContent.BringToFront();

			// Card 1: Windows Startup
			DoubleBufferedPanel pnlStartupCard = new DoubleBufferedPanel
			{
				Dock = DockStyle.Top,
				Height = 86,
				BackColor = BG_MAIN,
				Padding = new Padding(14, 10, 14, 10),
				Margin = new Padding(0, 0, 0, 12)
			};
			pnlStartupCard.Paint += delegate(object s, PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, pnlStartupCard.Width, pnlStartupCard.Height, 8f, BG_CARD, BORDER_CARD, 1f);
			};
			chkStartWithWindows = new CheckBox
			{
				Text = "Start DolphinXvdo with Windows",
				Font = UiTheme.DisplayFont(9.5f, FontStyle.Bold),
				ForeColor = UiTheme.TextPrimary,
				BackColor = BG_CARD,
				AutoSize = true,
				Location = new Point(14, 12),
				Cursor = Cursors.Hand,
				Checked = IsStartWithWindowsEnabled()
			};
			chkStartWithWindows.CheckedChanged += delegate
			{
				SetStartWithWindows(chkStartWithWindows.Checked);
			};
			Label lblStartupDesc = new Label
			{
				Text = "Automatically launches DolphinXvdo on Windows boot minimized to System Tray so audio stays equalized seamlessly.",
				Font = UiTheme.DisplayFont(8.2f),
				ForeColor = TEXT_MUTED,
				BackColor = BG_CARD,
				Location = new Point(14, 38),
				Size = new Size(498, 36)
			};
			pnlStartupCard.Controls.Add(chkStartWithWindows);
			pnlStartupCard.Controls.Add(lblStartupDesc);
			pnlContent.Controls.Add(pnlStartupCard);

			// Spacer
			Panel pnlSpacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = BG_MAIN };
			pnlContent.Controls.Add(pnlSpacer);

			// Card 2: Output Device Auto-Switch
			DoubleBufferedPanel pnlAutoCard = new DoubleBufferedPanel
			{
				Dock = DockStyle.Top,
				AutoSize = true,
				MinimumSize = new Size(0, 220),
				BackColor = BG_MAIN,
				Padding = new Padding(14, 12, 14, 14)
			};
			pnlAutoCard.Paint += delegate(object s, PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, pnlAutoCard.Width, pnlAutoCard.Height, 8f, BG_CARD, BORDER_CARD, 1f);
			};

			bool autoEnabled = (main != null) ? main.AutoSwitchPresetByDevice : true;
			chkAutoSwitchPreset = new CheckBox
			{
				Text = "Auto-Switch Preset by Output Device (สลับ Preset ตาม Output)",
				Font = UiTheme.DisplayFont(9.5f, FontStyle.Bold),
				ForeColor = UiTheme.TextPrimary,
				BackColor = BG_CARD,
				AutoSize = true,
				Location = new Point(14, 12),
				Cursor = Cursors.Hand,
				Checked = autoEnabled
			};
			chkAutoSwitchPreset.CheckedChanged += delegate
			{
				if (main != null)
				{
					main.AutoSwitchPresetByDevice = chkAutoSwitchPreset.Checked;
					main.SaveDevicePresetSettings();
				}
				if (pnlDeviceMappings != null)
				{
					pnlDeviceMappings.Enabled = chkAutoSwitchPreset.Checked;
				}
			};
			pnlAutoCard.Controls.Add(chkAutoSwitchPreset);

			Label lblAutoDesc = new Label
			{
				Text = "Assign a preset to each audio playback device. DolphinXvdo will automatically apply the preset when Windows switches to that sound output.",
				Font = UiTheme.DisplayFont(8.2f),
				ForeColor = TEXT_MUTED,
				BackColor = BG_CARD,
				Location = new Point(14, 36),
				Size = new Size(504, 28)
			};
			pnlAutoCard.Controls.Add(lblAutoDesc);

			pnlDeviceMappings = new Panel
			{
				Location = new Point(14, 70),
				Width = 504,
				AutoSize = true,
				BackColor = BG_CARD,
				Enabled = autoEnabled
			};

			PopulateDeviceMappings(pnlDeviceMappings);
			pnlAutoCard.Controls.Add(pnlDeviceMappings);

			pnlContent.Controls.Add(pnlAutoCard);
		}

		private void PopulateDeviceMappings(Panel container)
		{
			container.SuspendLayout();
			container.Controls.Clear();

			List<AudioDeviceInfo> allDevs = ModernAudioDevicesDialog.QueryAudioDevicesDirect();
			List<AudioDeviceInfo> renderDevs = allDevs.Where(d => d.Type == "Playback").ToList();

			List<string> presetNames = (main != null) ? main.GetAvailablePresetNames() : new List<string>();

			int y = 0;
			if (renderDevs.Count == 0)
			{
				Label lblNoDev = new Label
				{
					Text = "No active playback audio devices detected.",
					ForeColor = TEXT_MUTED,
					BackColor = BG_CARD,
					Location = new Point(0, y),
					AutoSize = true
				};
				container.Controls.Add(lblNoDev);
				container.ResumeLayout();
				return;
			}

			foreach (AudioDeviceInfo dev in renderDevs)
			{
				DoubleBufferedPanel row = new DoubleBufferedPanel
				{
					Location = new Point(0, y),
					Size = new Size(container.Width, 48),
					BackColor = BG_CARD,
					Margin = new Padding(0, 0, 0, 6)
				};
				row.Paint += delegate(object s, PaintEventArgs pe)
				{
					UiTheme.DrawRoundedSurface(pe.Graphics, row.Width, row.Height, 6f, UiTheme.BgWell, UiTheme.BorderRing, 1f);
				};

				string tagText = "OUT";
				string lower = (dev.Name + " " + dev.Adapter).ToLowerInvariant();
				if (lower.Contains("hdmi") || lower.Contains("display") || lower.Contains("vx") || lower.Contains("series") || lower.Contains("tv"))
				{
					tagText = "DSP";
				}
				else if (lower.Contains("speaker") || lower.Contains("ลำโพง"))
				{
					tagText = "SPK";
				}

				Label lblTag = new Label
				{
					Text = tagText,
					Font = UiTheme.MonoFont(7.2f, FontStyle.Bold),
					ForeColor = UiTheme.AccentBlue,
					BackColor = UiTheme.BgChip,
					Location = new Point(10, 14),
					Size = new Size(34, 20),
					TextAlign = ContentAlignment.MiddleCenter
				};
				row.Controls.Add(lblTag);

				// Device Name
				Label lblName = new Label
				{
					Text = dev.Name,
					Font = UiTheme.DisplayFont(9f, FontStyle.Bold),
					ForeColor = UiTheme.TextPrimary,
					BackColor = UiTheme.BgWell,
					Location = new Point(52, 6),
					Size = new Size(256, 18),
					AutoEllipsis = true
				};
				row.Controls.Add(lblName);

				// Adapter Name
				string sub = string.IsNullOrEmpty(dev.Adapter) ? dev.Id : dev.Adapter;
				Label lblSub = new Label
				{
					Text = sub,
					Font = UiTheme.MonoFont(7.2f),
					ForeColor = TEXT_MUTED,
					BackColor = UiTheme.BgWell,
					Location = new Point(52, 26),
					Size = new Size(256, 16),
					AutoEllipsis = true
				};
				row.Controls.Add(lblSub);

				// Preset ComboBox
				ComboBox cb = new ComboBox
				{
					DropDownStyle = ComboBoxStyle.DropDownList,
					Width = 178,
					Location = new Point(row.Width - 188, 12),
					BackColor = UiTheme.BgChip,
					ForeColor = UiTheme.TextPrimary,
					Font = UiTheme.DisplayFont(8.5f),
					FlatStyle = FlatStyle.Flat
				};
				cb.Items.Add("(No Change)");
				foreach (string p in presetNames)
				{
					cb.Items.Add(p);
				}

				// Find selected mapped preset
				string mappedPreset = null;
				if (main != null && main.DevicePresetMap != null)
				{
					if (main.DevicePresetMap.ContainsKey(dev.Id))
					{
						mappedPreset = main.DevicePresetMap[dev.Id];
					}
					else if (main.DevicePresetMap.ContainsKey(dev.Name))
					{
						mappedPreset = main.DevicePresetMap[dev.Name];
					}
				}

				if (!string.IsNullOrEmpty(mappedPreset) && cb.Items.Contains(mappedPreset))
				{
					cb.SelectedItem = mappedPreset;
				}
				else
				{
					cb.SelectedIndex = 0; // (No Change)
				}

				string currentDevId = dev.Id;
				string currentDevName = dev.Name;
				cb.SelectedIndexChanged += delegate
				{
					if (main != null && cb.SelectedItem != null)
					{
						string sel = cb.SelectedItem.ToString();
						if (sel.Equals("(No Change)", StringComparison.OrdinalIgnoreCase))
						{
							main.DevicePresetMap.Remove(currentDevId);
							main.DevicePresetMap.Remove(currentDevName);
						}
						else
						{
							main.DevicePresetMap[currentDevId] = sel;
							main.DevicePresetMap[currentDevName] = sel;
						}
						main.SaveDevicePresetSettings();
					}
				};

				row.Controls.Add(cb);
				container.Controls.Add(row);
				y += 54;
			}

			container.Height = y;
			container.ResumeLayout();
		}

		private bool IsStartWithWindowsEnabled()
		{
			try
			{
				using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
				{
					if (registryKey != null && registryKey.GetValue("DolphinXVdo") != null)
					{
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private void SetStartWithWindows(bool enable)
		{
			try
			{
				string executablePath = Application.ExecutablePath;
				string startupShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DolphinXvdo.lnk");

				// Ensure any legacy startup folder shortcut is cleaned up
				try
				{
					if (File.Exists(startupShortcut))
					{
						File.Delete(startupShortcut);
					}
				}
				catch
				{
				}

				if (enable)
				{
					// Registry HKCU Run
					using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
					{
						if (registryKey != null)
						{
							registryKey.SetValue("DolphinXVdo", "\"" + executablePath + "\" --minimized");
						}
					}
				}
				else
				{
					// Remove Registry HKCU Run
					using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
					{
						if (registryKey != null && registryKey.GetValue("DolphinXVdo") != null)
						{
							registryKey.DeleteValue("DolphinXVdo", false);
						}
					}

					// Remove legacy scheduled task if it was previously created
					try
					{
						using (Process p = new Process())
						{
							p.StartInfo.FileName = "schtasks.exe";
							p.StartInfo.Arguments = "/delete /tn \"DolphinXvdo_Autostart\" /f";
							p.StartInfo.UseShellExecute = false;
							p.StartInfo.CreateNoWindow = true;
							p.Start();
							p.WaitForExit(2000);
						}
					}
					catch
					{
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Could not update Windows startup configuration:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}
	}

	public class ModernVerticalSlider : Control
	{
		private static readonly Pen penTrackOuter6 = new Pen(Color.FromArgb(30, 30, 30), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		private static readonly Pen penTrackBorder2 = new Pen(Color.FromArgb(16, 16, 16), 2f);
		private static readonly Pen penCenterLine = new Pen(Color.FromArgb(68, 68, 68), 1.2f);
		private static readonly Pen penThumbGrip = new Pen(Color.FromArgb(36, 36, 36), 1f);
		private static readonly SolidBrush brushThumbGrip = new SolidBrush(Color.FromArgb(38, 82, 168, 255));
		private static readonly Pen penThumbBorderHover = new Pen(UiTheme.AccentBlue, 1.2f);
		private static readonly Pen penThumbBorderNormal = new Pen(Color.FromArgb(78, 78, 78), 1f);
		private static readonly Pen penGainGreen3 = new Pen(UiTheme.AccentGreen, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		private static readonly Pen penGainBlue3 = new Pen(UiTheme.AccentBlue, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		private static readonly Pen penGainNeutral3 = new Pen(UiTheme.AccentAlert, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		private static readonly Pen penGainGreen2 = new Pen(UiTheme.AccentGreen, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		private static readonly Pen penGainBlue2 = new Pen(UiTheme.AccentBlue, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		private static readonly Pen penGainNeutral2 = new Pen(UiTheme.AccentAlert, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

		private double _value;

		private bool isDragging = false;

		private bool isHovered = false;

		public double MinValue
		{
			get;
			set;
		}

		public double MaxValue
		{
			get;
			set;
		}

		public double Value
		{
			get
			{
				return _value;
			}
			set
			{
				double num = Math.Max(MinValue, Math.Min(MaxValue, value));
				if (Math.Abs(_value - num) > 0.001)
				{
					_value = num;
					Invalidate();
					if (this.ValueChanged != null)
					{
						this.ValueChanged(this, EventArgs.Empty);
					}
				}
			}
		}

		public event EventHandler ValueChanged;

		public ModernVerticalSlider()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
			BackColor = Color.Transparent;
			MinValue = -30.0;
			MaxValue = 30.0;
			_value = 0.0;
			Cursor = Cursors.Hand;
			EventHandler value = delegate
			{
				isHovered = true;
				Invalidate();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				isHovered = false;
				Invalidate();
			};
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				isDragging = true;
				UpdateValueFromMouse(e.Y);
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (isDragging)
			{
				UpdateValueFromMouse(e.Y);
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			isDragging = false;
			base.OnMouseUp(e);
		}

		private void UpdateValueFromMouse(int mouseY)
		{
			int num = 10;
			int num2 = base.Height - 10;
			int num3 = num2 - num;
			if (num3 > 0)
			{
				float val = 1f - (float)(mouseY - num) / (float)num3;
				val = Math.Max(0f, Math.Min(1f, val));
				Value = Math.Round((MinValue + (double)val * (MaxValue - MinValue)) * 2.0) / 2.0;
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			int num = base.Width / 2;
			int num2 = 10;
			int num3 = base.Height - 10;
			int num4 = num3 - num2;
			if (num4 <= 0)
			{
				return;
			}
			graphics.DrawLine(penTrackOuter6, num, num2, num, num3);
			graphics.DrawLine(penTrackBorder2, num, num2, num, num3);
			float num5 = (float)((0.0 - MinValue) / (MaxValue - MinValue));
			float num6 = (float)num3 - num5 * (float)num4;
			graphics.DrawLine(penCenterLine, num - 7, num6, num + 7, num6);
			float num7 = (float)((_value - MinValue) / (MaxValue - MinValue));
			float num8 = (float)num3 - num7 * (float)num4;
			Pen activePen3 = ((_value > 0.0) ? penGainGreen3 : ((_value < 0.0) ? penGainBlue3 : penGainNeutral3));
			Pen activePen2 = ((_value > 0.0) ? penGainGreen2 : ((_value < 0.0) ? penGainBlue2 : penGainNeutral2));
			if (Math.Abs(num8 - num6) > 1f)
			{
				graphics.DrawLine(activePen3, num, num6, num, num8);
			}
			int num9 = Math.Max(16, Math.Min(base.Width - 6, 22));
			int num10 = 14;
			RectangleF thumbRect = new RectangleF((float)num - (float)num9 / 2f, num8 - (float)num10 / 2f, (float)num9, (float)num10);
			if (isDragging || isHovered)
			{
				RectangleF bounds = new RectangleF(thumbRect.X - 2.5f, thumbRect.Y - 2f, thumbRect.Width + 5f, thumbRect.Height + 4f);
				using (GraphicsPath pathGlow = UiTheme.CreateRoundedPath(bounds, 5f))
				{
					graphics.FillPath(brushThumbGrip, pathGlow);
				}
			}
			Pen borderPen = ((isDragging || isHovered) ? penThumbBorderHover : penThumbBorderNormal);
			using (LinearGradientBrush brush2 = new LinearGradientBrush(thumbRect, Color.FromArgb(34, 34, 34), Color.FromArgb(16, 16, 16), LinearGradientMode.Vertical))
			using (GraphicsPath pathThumb = UiTheme.CreateRoundedPath(thumbRect, 4f))
			{
				graphics.FillPath(brush2, pathThumb);
				graphics.DrawPath(borderPen, pathThumb);
			}
			graphics.DrawLine(penThumbGrip, thumbRect.X + 3f, thumbRect.Y + 3f, thumbRect.Right - 3f, thumbRect.Y + 3f);
			graphics.DrawLine(penThumbGrip, thumbRect.X + 3f, thumbRect.Bottom - 3f, thumbRect.Right - 3f, thumbRect.Bottom - 3f);
			graphics.DrawLine(activePen2, thumbRect.X + 3f, num8, thumbRect.Right - 3f, num8);
		}
	}
	public class EqRulerControl : Control
	{
		public EqRulerControl()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
			BackColor = Color.Transparent;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			int num = 10;
			int num2 = base.Height - 10;
			int num3 = num2 - num;
			if (num3 <= 0)
			{
				return;
			}
			int num4 = base.Width - 2;
			using (Font font = UiTheme.MonoFont(6.3f, FontStyle.Regular))
			using (SolidBrush brush = new SolidBrush(UiTheme.TextMuted))
			using (Pen pen2 = new Pen(Color.FromArgb(42, 42, 42), 1f))
			using (Pen pen = new Pen(Color.FromArgb(88, 88, 88), 1.3f))
			using (StringFormat format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
			{
				double[] array = new double[5] { 30.0, 15.0, 0.0, -15.0, -30.0 };
				foreach (double num5 in array)
				{
					float num6 = 1f - (float)((num5 - -30.0) / 60.0);
					float num7 = (float)num + num6 * (float)num3;
					bool flag = num5 == 0.0;
					int num8 = (flag ? 6 : 4);
					Pen pen3 = (flag ? pen : pen2);
					graphics.DrawLine(pen3, num4 - num8, num7, num4, num7);
					string s = (flag ? "0 dB" : ((num5 > 0.0) ? ("+" + (int)num5) : ((int)num5).ToString()));
					graphics.DrawString(s, font, brush, new RectangleF(0f, num7 - 8f, (float)(num4 - num8 - 2), 16f), format);
				}
			}
		}
	}
	public class ModernHorizontalSlider : Control
	{
		private double _value;

		private bool isDragging = false;

		private bool isHovered = false;

		public double MinValue
		{
			get;
			set;
		}

		public double MaxValue
		{
			get;
			set;
		}

		public double Value
		{
			get
			{
				return _value;
			}
			set
			{
				double num = Math.Max(MinValue, Math.Min(MaxValue, value));
				if (Math.Abs(_value - num) > 0.001)
				{
					_value = num;
					Invalidate();
					if (this.ValueChanged != null)
					{
						this.ValueChanged(this, EventArgs.Empty);
					}
				}
			}
		}

		public int DecimalPlaces
		{
			get;
			set;
		}

		public event EventHandler ValueChanged;

		public ModernHorizontalSlider()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
			BackColor = Color.Transparent;
			MinValue = -60.0;
			MaxValue = 0.0;
			_value = -18.0;
			DecimalPlaces = 1;
			Cursor = Cursors.Hand;
			base.Height = 24;
			EventHandler value = delegate
			{
				isHovered = true;
				Invalidate();
			};
			base.MouseEnter += value;
			base.MouseLeave += delegate
			{
				isHovered = false;
				Invalidate();
			};
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				isDragging = true;
				UpdateValueFromMouse(e.X);
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (isDragging)
			{
				UpdateValueFromMouse(e.X);
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			isDragging = false;
			base.OnMouseUp(e);
		}

		private void UpdateValueFromMouse(int mouseX)
		{
			int num = 9;
			int num2 = base.Width - 9;
			int num3 = num2 - num;
			if (num3 > 0)
			{
				float val = (float)(mouseX - num) / (float)num3;
				val = Math.Max(0f, Math.Min(1f, val));
				double value = MinValue + (double)val * (MaxValue - MinValue);
				Value = Math.Round(value, DecimalPlaces);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			float cy = (float)base.Height / 2f;
			float xStart = 9f;
			float xEnd = (float)base.Width - 9f;
			float span = xEnd - xStart;
			if (span <= 0f)
			{
				return;
			}
			using (Pen pen = new Pen(UiTheme.BgChip, 4f))
			{
				pen.StartCap = LineCap.Round;
				pen.EndCap = LineCap.Round;
				graphics.DrawLine(pen, xStart, cy, xEnd, cy);
			}
			float frac = (float)((_value - MinValue) / (MaxValue - MinValue));
			float thumbX = xStart + frac * span;
			Color activeColor = UiTheme.AccentBlue;
			if (MinValue < 0.0 && MaxValue > 0.0)
			{
				float zeroFrac = (float)((0.0 - MinValue) / (MaxValue - MinValue));
				float zeroX = xStart + zeroFrac * span;
				activeColor = ((_value > 0.0) ? UiTheme.AccentGreen : UiTheme.AccentBlue);
				using (Pen pen2 = new Pen(activeColor, 4f))
				{
					pen2.StartCap = LineCap.Round;
					pen2.EndCap = LineCap.Round;
					graphics.DrawLine(pen2, zeroX, cy, thumbX, cy);
				}
			}
			else
			{
				using (Pen pen2 = new Pen(activeColor, 4f))
				{
					pen2.StartCap = LineCap.Round;
					pen2.EndCap = LineCap.Round;
					graphics.DrawLine(pen2, xStart, cy, thumbX, cy);
				}
			}
			float r = 6f;
			if (isDragging || isHovered)
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(42, activeColor.R, activeColor.G, activeColor.B)))
				{
					graphics.FillEllipse(brush, thumbX - 10f, cy - 10f, 20f, 20f);
				}
			}
			using (SolidBrush brush2 = new SolidBrush(UiTheme.BgCard))
			{
				graphics.FillEllipse(brush2, thumbX - r, cy - r, r * 2f, r * 2f);
			}
			using (SolidBrush brush3 = new SolidBrush(UiTheme.TextPrimary))
			{
				graphics.FillEllipse(brush3, thumbX - 2.2f, cy - 2.2f, 4.4f, 4.4f);
			}
			Color ringColor = ((isDragging || isHovered) ? UiTheme.TextPrimary : activeColor);
			using (Pen pen3 = new Pen(ringColor, 1.8f))
			{
				graphics.DrawEllipse(pen3, thumbX - r, cy - r, r * 2f, r * 2f);
			}
		}
	}
	public class ApoPowerButton : Control
	{
		private bool _checked;

		private bool _hovered;

		private bool _pressed;

		private ToolTip toolTip;

		public bool Checked
		{
			get
			{
				return _checked;
			}
			set
			{
				if (_checked != value)
				{
					_checked = value;
					Invalidate();
					if (this.CheckedChanged != null)
					{
						this.CheckedChanged(this, EventArgs.Empty);
					}
				}
			}
		}

		public event EventHandler CheckedChanged;

		public ApoPowerButton()
		{
			_checked = true;
			_hovered = false;
			_pressed = false;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
			base.Size = new Size(28, 28);
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			try
			{
				toolTip = new ToolTip();
				toolTip.SetToolTip(this, "Click to Toggle Power (ON / OFF)");
			}
			catch
			{
			}
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			_hovered = true;
			Invalidate();
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_hovered = false;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				_pressed = true;
				Invalidate();
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			if (_pressed && e.Button == MouseButtons.Left)
			{
				_pressed = false;
				Checked = !Checked;
			}
			base.OnMouseUp(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			float diameter = Math.Max(14f, (float)Math.Min(base.Width, base.Height) - 4f);
			RectangleF rect = new RectangleF(((float)base.Width - diameter) / 2f, ((float)base.Height - diameter) / 2f, diameter, diameter);
			Color color;
			Color color2;
			Color color3;
			if (_checked)
			{
				color = (_pressed ? Color.FromArgb(24, 48, 28) : (_hovered ? Color.FromArgb(20, 42, 24) : Color.FromArgb(16, 34, 20)));
				color2 = (_hovered ? Color.FromArgb(128, 212, 142) : UiTheme.AccentGreen);
				color3 = (_hovered ? UiTheme.TextPrimary : UiTheme.AccentGreen);
			}
			else
			{
				color = (_pressed ? UiTheme.BgPressed : (_hovered ? UiTheme.BgChip : UiTheme.BgWell));
				color2 = (_hovered ? UiTheme.AccentBlue : UiTheme.BorderRing);
				color3 = (_hovered ? UiTheme.TextBody : UiTheme.TextMuted);
			}
			using (SolidBrush brush = new SolidBrush(color))
			{
				graphics.FillEllipse(brush, rect);
			}
			float borderW = Math.Max(1.3f, diameter * 0.055f);
			using (Pen pen = new Pen(color2, borderW))
			{
				graphics.DrawEllipse(pen, rect);
			}
			float cx = rect.X + rect.Width / 2f;
			float cy = rect.Y + rect.Height / 2f + 0.4f;
			float iconR = rect.Width * 0.27f;
			float strokeW = Math.Max(1.8f, rect.Width * 0.08f);
			using (Pen pen2 = new Pen(color3, strokeW))
			{
				pen2.StartCap = LineCap.Round;
				pen2.EndCap = LineCap.Round;
				graphics.DrawArc(pen2, cx - iconR, cy - iconR, iconR * 2f, iconR * 2f, -60f, 300f);
				graphics.DrawLine(pen2, cx, cy - iconR * 1.18f, cx, cy + 0.2f);
			}
		}
	}
	public class MainForm : Form
	{
		private const int WM_SETICON = 128;

		private const int ICON_SMALL = 0;

		private const int ICON_BIG = 1;

		private const int WM_NCLBUTTONDOWN = 161;

		private const int HTCAPTION = 2;

		private const int WM_NCHITTEST = 132;

		private const int HTLEFT = 10;

		private const int HTRIGHT = 11;

		private const int HTTOP = 12;

		private const int HTTOPLEFT = 13;

		private const int HTTOPRIGHT = 14;

		private const int HTBOTTOM = 15;

		private const int HTBOTTOMLEFT = 16;

		private const int HTBOTTOMRIGHT = 17;

		private const int RESIZE_BORDER = 6;

		private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

		private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

		private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

		private const int DWMWA_BORDER_COLOR = 34;

		private const int DWMWA_CAPTION_COLOR = 35;

		private const int DWMWA_TEXT_COLOR = 36;

		private Panel pnlTitleBar;

		private Color BG_MAIN = UiTheme.BgCanvas;

		private Color BG_CARD = UiTheme.BgCard;

		private Color BG_INPUT = UiTheme.BgWell;

		private Color BORDER_CARD = UiTheme.BorderRing;

		private Color ACCENT_CYAN = UiTheme.AccentBlue;

		private Color ACCENT_GREEN = UiTheme.AccentGreen;

		private Color TEXT_MUTED = UiTheme.TextMuted;

		private double preampDb = 0.0;

		private bool masterBypass = false;

		private static readonly double[] Default10Frequencies = new double[10]
		{
			31.0,
			63.0,
			125.0,
			250.0,
			500.0,
			1000.0,
			2000.0,
			4000.0,
			8000.0,
			16000.0
		};

		private static readonly double[] Extended15Frequencies = new double[15]
		{
			31.0,
			63.0,
			125.0,
			250.0,
			500.0,
			1000.0,
			2000.0,
			4000.0,
			8000.0,
			16000.0,
			20000.0,
			20.0,
			1400.0,
			2800.0,
			5600.0
		};

		private static readonly double[] graphFreqMarks = new double[] { 50.0, 100.0, 250.0, 500.0, 1000.0, 2000.0, 4000.0, 8000.0, 16000.0 };
		private static readonly Pen penGridDash = new Pen(Color.FromArgb(48, 48, 48), 1f) { DashStyle = DashStyle.Dash };
		private static readonly Pen penBypassRed = new Pen(UiTheme.TextMuted, 1.8f) { DashStyle = DashStyle.Dash };
		private static readonly Pen penCurveCyan = new Pen(UiTheme.AccentBlue, 2.2f);

		private List<BiquadFilter> filters = new List<BiquadFilter>();

		private ModernHorizontalSlider sliderPreamp;

		private TextBox txtPreampVal;

		private ModernActionButton btnAutoPreamp;

		private List<ModernVerticalSlider> bandSliders = new List<ModernVerticalSlider>();

		private List<TextBox> bandGainBoxes = new List<TextBox>();

		private List<TextBox> bandFreqBoxes = new List<TextBox>();

		private List<TextBox> bandQBoxes = new List<TextBox>();

		private TableLayoutPanel tlpBands;

		private Label lblEqTitle;

		private ModernActionButton btnAddBand;

		private ModernActionButton btnRemoveBand;

		private Panel pnlGraph;

		private ModernBypassButton chkMasterBypass;

		private ModernDropDownList cbPresets;

		private ApoPowerButton btnCompPower;

		private CheckBox chkCompPower = new CheckBox
		{
			Checked = true
		};

		public bool CompAutoMakeup = false;

		private ModernHorizontalSlider sliderThresh;

		private TextBox txtThreshVal;

		private ModernVuMeterControl vuMeter;

		private System.Windows.Forms.Timer vuTimer;

		private IAudioMeterInformation wasapiMeter;

		private IMMDevice currentAudioDevice;

		private int meterChannelCount = 2;

		private IntPtr pMeterChannelsMem = IntPtr.Zero;

		private ApoPowerButton btnEqPower;

		public double CompRatio = 15.0;

		public double CompWet = 0.0;

		public double CompDry = -32.0;

		public double CompPreComp = 2.0;

		public double CompKnee = 0.0;

		public double CompAttack = 5.0;

		public double CompRelease = 160.0;

		public double CompRms = 0.0;

		public bool CompClassicAttack = false;

		public bool CompAutoRelease = false;

		private NotifyIcon trayIcon;

		private ContextMenuStrip trayMenu;

		private bool allowClose = false;

		private bool shownTrayBalloon = false;

		private bool startMinimizedOnLaunch = false;

		private bool hasShownOnce = false;

		private System.Windows.Forms.Timer syncTimer;
		private System.Windows.Forms.Timer stateSaveTimer;
		private System.Windows.Forms.Timer usageTimer;
		private Label lblStatusUsage;
		private Label lblEstPeakGain;
		private Label lblEstLatency;
		private Label lblEstInitTime;
		private Label lblEstCpuUsage;
		private double lastMeasuredInitTimeMs = 3.2;
		private double lastCalculatedCoreCpu = 1.2;
		public bool AutoSwitchPresetByDevice = true;
		public Dictionary<string, string> DevicePresetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private string lastDetectedDefaultDeviceId = null;
		private System.Windows.Forms.Timer deviceCheckTimer = null;
		private ModernActionButton btnVstRack;
		public List<CustomVstItem> CustomVstPlugins = new List<CustomVstItem>();
		private int lastSyncTick = 0;
		private const int SYNC_THROTTLE_MS = 25;
		private bool syncPending = false;
		private string lastSyncedConfig = null;
		private static List<string> cachedTargetConfigPaths = null;
		private Font fontGraph = UiTheme.MonoFont(6.4f);

		private bool isInitializing = false;
		private bool isRebuildingBands = false;
		private float[] cachedMeterBuffer = null;
		private double cachedPeakGain = 0.0;
		private bool isPeakGainDirty = true;

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern int RegisterWindowMessage(string lpString);

		private static readonly int WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern bool ReleaseCapture();

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

		[DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool DestroyIcon(IntPtr hIcon);

		public static void ApplyDiscordDarkStyle(IntPtr handle)
		{
			try
			{
				int attrValue = 1;
				if (DwmSetWindowAttribute(handle, 20, ref attrValue, 4) != 0)
				{
					DwmSetWindowAttribute(handle, 19, ref attrValue, 4);
				}
				int attrValue2 = 0x00000000;
				DwmSetWindowAttribute(handle, 35, ref attrValue2, 4);
				int attrValue3 = 0x00262626;
				DwmSetWindowAttribute(handle, 34, ref attrValue3, 4);
				int attrValue4 = 16777215;
				DwmSetWindowAttribute(handle, 36, ref attrValue4, 4);
				int attrValue5 = 2;
				DwmSetWindowAttribute(handle, 33, ref attrValue5, 4);
			}
			catch
			{
			}
		}

		private bool dwmRoundedCornersActive = false;

		private void UpdateWindowCornerStyle()
		{
			if (!base.IsHandleCreated)
			{
				return;
			}
			try
			{
				int darkMode = 1;
				if (DwmSetWindowAttribute(base.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, 4) != 0)
				{
					DwmSetWindowAttribute(base.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, 4);
				}
				int cornerPref = ((base.WindowState == FormWindowState.Maximized) ? 1 : 2);
				int hr = DwmSetWindowAttribute(base.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, 4);
				dwmRoundedCornersActive = (hr == 0);
				int borderColor = 0x002C2C2C;
				DwmSetWindowAttribute(base.Handle, DWMWA_BORDER_COLOR, ref borderColor, 4);
			}
			catch
			{
				dwmRoundedCornersActive = false;
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			try
			{
				if (base.Icon != null)
				{
					SendMessage(base.Handle, 128, (IntPtr)0, base.Icon.Handle);
					SendMessage(base.Handle, 128, (IntPtr)1, base.Icon.Handle);
				}
				UpdateWindowCornerStyle();
				if (dwmRoundedCornersActive)
				{
					base.Padding = Padding.Empty;
				}
			}
			catch
			{
			}
		}

		protected override void WndProc(ref Message m)
		{
			if (Program.WM_SHOW_DOLPHINXVDO != 0 && m.Msg == Program.WM_SHOW_DOLPHINXVDO)
			{
				try
				{
					if (base.InvokeRequired)
					{
						base.BeginInvoke(new Action(ShowMainWindow));
					}
					else
					{
						ShowMainWindow();
					}
				}
				catch
				{
				}
				m.Result = (IntPtr)1;
				return;
			}
			if (WM_TASKBARCREATED != 0 && m.Msg == WM_TASKBARCREATED)
			{
				try
				{
					if (trayIcon != null)
					{
						trayIcon.Visible = false;
						trayIcon.Visible = true;
					}
				}
				catch
				{
				}
			}
			if (m.Msg == 132)
			{
				base.WndProc(ref m);
				Point point = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, (m.LParam.ToInt32() >> 16) & 0xFFFF));
				if (point.Y <= 6)
				{
					if (point.X <= 6)
					{
						m.Result = (IntPtr)13;
					}
					else if (point.X >= base.ClientSize.Width - 6)
					{
						m.Result = (IntPtr)14;
					}
					else
					{
						m.Result = (IntPtr)12;
					}
				}
				else if (point.Y >= base.ClientSize.Height - 6)
				{
					if (point.X <= 6)
					{
						m.Result = (IntPtr)16;
					}
					else if (point.X >= base.ClientSize.Width - 6)
					{
						m.Result = (IntPtr)17;
					}
					else
					{
						m.Result = (IntPtr)15;
					}
				}
				else if (point.X <= 6)
				{
					m.Result = (IntPtr)10;
				}
				else if (point.X >= base.ClientSize.Width - 6)
				{
					m.Result = (IntPtr)11;
				}
			}
			else
			{
				base.WndProc(ref m);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			if (base.WindowState != FormWindowState.Maximized && !dwmRoundedCornersActive)
			{
				using (Pen pen = new Pen(Color.FromArgb(42, 42, 42), 1f))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, base.ClientSize.Width - 1, base.ClientSize.Height - 1);
				}
			}
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			UpdateWindowCornerStyle();
			if (base.WindowState == FormWindowState.Maximized || dwmRoundedCornersActive)
			{
				base.Padding = Padding.Empty;
			}
			else
			{
				base.Padding = new Padding(1);
			}
			if (base.WindowState == FormWindowState.Minimized)
			{
				if (usageTimer != null) usageTimer.Stop();
				if (vuTimer != null) vuTimer.Stop();
			}
			else
			{
				if (usageTimer != null && !usageTimer.Enabled) usageTimer.Start();
				if (vuTimer != null && !vuTimer.Enabled) vuTimer.Start();
			}
			Invalidate();
		}

		private void TitleBar_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(base.Handle, 161, (IntPtr)2, IntPtr.Zero);
			}
		}

		private void TitleBar_DoubleClick(object sender, EventArgs e)
		{
			if (base.WindowState == FormWindowState.Maximized)
			{
				base.WindowState = FormWindowState.Normal;
			}
			else
			{
				base.WindowState = FormWindowState.Maximized;
			}
		}

		public static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			int num = radius * 2;
			graphicsPath.AddArc(bounds.X, bounds.Y, num, num, 180f, 90f);
			graphicsPath.AddArc(bounds.Right - num, bounds.Y, num, num, 270f, 90f);
			graphicsPath.AddArc(bounds.Right - num, bounds.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(bounds.X, bounds.Bottom - num, num, num, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}

		public MainForm(bool startMinimized = false)
		{
			startMinimizedOnLaunch = startMinimized;
			base.WindowState = FormWindowState.Normal;
			base.ClientSize = new Size(1060, 680);
			Text = "DolphinXvdo v2.3";
			BackColor = BG_MAIN;
			isInitializing = true;
			EnsureStartupConfig();
			EnsureReaCompInstalled();
			EnsureDefaultPresetExists();
			InitializeFilters();
			InitializeComponent();
			InitializeTrayIcon();
			LoadSettingsAfterUI();
			LoadDevicePresetSettings();
			InitializeDeviceCheckTimer();
			this.AllowDrop = true;
			this.DragEnter += MainForm_DragEnter;
			this.DragDrop += MainForm_DragDrop;
			isInitializing = false;
			SyncToAPO();
		}

		private void EnsureStartupConfig()
		{
			try
			{
				using (Process p = new Process())
				{
					p.StartInfo.FileName = "schtasks.exe";
					p.StartInfo.Arguments = "/delete /tn \"DolphinXvdo_Autostart\" /f";
					p.StartInfo.UseShellExecute = false;
					p.StartInfo.CreateNoWindow = true;
					p.Start();
					p.WaitForExit(1000);
				}
			}
			catch
			{
			}

			try
			{
				string startupShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DolphinXvdo.lnk");
				if (File.Exists(startupShortcut))
				{
					File.Delete(startupShortcut);
				}
			}
			catch
			{
			}

			try
			{
				using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
				{
					if (registryKey != null && registryKey.GetValue("DolphinXVdo") != null)
					{
						registryKey.SetValue("DolphinXVdo", "\"" + Application.ExecutablePath + "\" --minimized");
					}
				}
			}
			catch
			{
			}
		}

		protected override void SetVisibleCore(bool value)
		{
			if (startMinimizedOnLaunch && !hasShownOnce)
			{
				value = false;
				if (!base.IsHandleCreated)
				{
					CreateHandle();
				}
			}
			base.SetVisibleCore(value);
		}

		public void OpenSettingsDialog()
		{
			using (SettingsDialog settingsDialog = new SettingsDialog(this))
			{
				settingsDialog.ShowDialog(this);
			}
		}

		private void InitializeFilters()
		{
			filters.Clear();
			for (int i = 0; i < Default10Frequencies.Length; i++)
			{
				filters.Add(new BiquadFilter("PK", Default10Frequencies[i], 0.0, 1.41));
			}
		}

		private void InitializeComponent()
		{
			Text = "DolphinXvdo v2.3";
			base.ShowInTaskbar = true;
			try
			{
				string text = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
				if (!System.IO.File.Exists(text))
				{
					text = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logo", "logo.ico");
				}
				if (!System.IO.File.Exists(text))
				{
					text = "C:\\DolphinY NOOB\\logo\\logo.ico";
				}
				if (System.IO.File.Exists(text))
				{
					base.Icon = new System.Drawing.Icon(text);
				}
				else
				{
					string text2 = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logo", "logo.png");
					if (!System.IO.File.Exists(text2))
					{
						text2 = "C:\\DolphinY NOOB\\logo\\logo.png";
					}
					if (System.IO.File.Exists(text2))
					{
						using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(text2))
						{
							IntPtr hIcon = bitmap.GetHicon();
							try
							{
								using (System.Drawing.Icon tempIcon = System.Drawing.Icon.FromHandle(hIcon))
								{
									base.Icon = (System.Drawing.Icon)tempIcon.Clone();
								}
							}
							finally
							{
								DestroyIcon(hIcon);
							}
						}
					}
				}
			}
			catch
			{
			}
			base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			DoubleBuffered = true;
			base.ResizeRedraw = true;
			base.Padding = new System.Windows.Forms.Padding(1);
			base.Size = new System.Drawing.Size(780, 808);
			MinimumSize = new System.Drawing.Size(740, 768);
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			BackColor = BG_MAIN;
			ForeColor = UiTheme.TextBody;
			Font = UiTheme.DisplayFont(9f, System.Drawing.FontStyle.Regular);
			syncTimer = new System.Windows.Forms.Timer
			{
				Interval = 25
			};
			syncTimer.Tick += delegate
			{
				syncTimer.Stop();
				if (syncPending)
				{
					syncPending = false;
					lastSyncTick = Environment.TickCount;
					SyncAudioConfigOnly();
				}
			};
			stateSaveTimer = new System.Windows.Forms.Timer
			{
				Interval = 350
			};
			stateSaveTimer.Tick += delegate
			{
				stateSaveTimer.Stop();
				SaveDedicatedState();
			};
			usageTimer = new System.Windows.Forms.Timer
			{
				Interval = 1500
			};
			usageTimer.Tick += delegate
			{
				UpdateUsageMetrics();
			};
			usageTimer.Start();
			pnlTitleBar = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 52,
				BackColor = BG_MAIN,
				Padding = new System.Windows.Forms.Padding(14, 0, 14, 0)
			};
			pnlTitleBar.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				using (System.Drawing.Pen pen = new System.Drawing.Pen(UiTheme.BorderSubtle, 1f))
				{
					e.Graphics.DrawLine(pen, 0, pnlTitleBar.Height - 1, pnlTitleBar.Width, pnlTitleBar.Height - 1);
				}
			};
			pnlTitleBar.MouseDown += new System.Windows.Forms.MouseEventHandler(TitleBar_MouseDown);
			pnlTitleBar.DoubleClick += new System.EventHandler(TitleBar_DoubleClick);
			base.Controls.Add(pnlTitleBar);
			System.Windows.Forms.Panel panel2 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Left,
				Width = 310,
				BackColor = System.Drawing.Color.Transparent
			};
			panel2.MouseDown += new System.Windows.Forms.MouseEventHandler(TitleBar_MouseDown);
			panel2.DoubleClick += new System.EventHandler(TitleBar_DoubleClick);
			string text3 = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logo", "logo.png");
			if (!System.IO.File.Exists(text3))
			{
				text3 = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logo.png");
			}
			if (!System.IO.File.Exists(text3))
			{
				text3 = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "logo", "logo.png");
			}
			if (!System.IO.File.Exists(text3))
			{
				text3 = "C:\\King Hermes\\DolphinXVDO\\logo\\logo.png";
			}
			int num = 0;
			if (System.IO.File.Exists(text3))
			{
				try
				{
					System.Windows.Forms.PictureBox pictureBox = new System.Windows.Forms.PictureBox();
					using (System.IO.FileStream fs = new System.IO.FileStream(text3, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
					using (System.Drawing.Image tempImg = System.Drawing.Image.FromStream(fs))
					{
						pictureBox.Image = new System.Drawing.Bitmap(tempImg);
					}
					pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
					pictureBox.Size = new System.Drawing.Size(32, 32);
					pictureBox.Location = new System.Drawing.Point(0, 10);
					pictureBox.BackColor = System.Drawing.Color.Transparent;
					panel2.Controls.Add(pictureBox);
					num = 40;
				}
				catch
				{
				}
			}
			System.Drawing.Font titleFont = UiTheme.DisplayFont(13.5f, System.Drawing.FontStyle.Bold);
			System.Windows.Forms.Label value2 = new System.Windows.Forms.Label
			{
				Text = "DolphinXvdo",
				Font = titleFont,
				ForeColor = UiTheme.TextPrimary,
				BackColor = System.Drawing.Color.Transparent,
				AutoSize = true,
				Location = new System.Drawing.Point(num, 13)
			};
			value2.MouseDown += new System.Windows.Forms.MouseEventHandler(TitleBar_MouseDown);
			panel2.Controls.Add(value2);
			int x = num + System.Windows.Forms.TextRenderer.MeasureText("DolphinXvdo", titleFont).Width + 8;
			ModernStatusPill versionPill = new ModernStatusPill("V2.3")
			{
				Location = new System.Drawing.Point(x, 15),
				Size = new System.Drawing.Size(56, 22),
				DotColor = UiTheme.AccentGreen,
				PillBackColor = UiTheme.BgChip,
				PillBorderColor = UiTheme.BorderRing,
				PillTextColor = UiTheme.TextMuted
			};
			panel2.Controls.Add(versionPill);
			pnlTitleBar.Controls.Add(panel2);
			System.Windows.Forms.Panel panel4 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Right,
				Width = 374,
				BackColor = System.Drawing.Color.Transparent
			};
			panel4.MouseDown += new System.Windows.Forms.MouseEventHandler(TitleBar_MouseDown);
			panel4.DoubleClick += new System.EventHandler(TitleBar_DoubleClick);
			DolphinXVDO.ModernActionButton modernActionButton2 = new DolphinXVDO.ModernActionButton
			{
				Text = "Audio Devices Setup",
				VectorIcon = "speaker",
				Location = new System.Drawing.Point(0, 11),
				Size = new System.Drawing.Size(166, 30),
				CornerRadius = 6,
				NormalBackColor = UiTheme.CtaBg,
				HoverBackColor = UiTheme.CtaHoverBg,
				PressedBackColor = UiTheme.CtaPressedBg,
				NormalBorderColor = UiTheme.CtaBg,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.CtaText,
				HoverTextColor = UiTheme.CtaText,
				Font = UiTheme.DisplayFont(8.4f, System.Drawing.FontStyle.Bold)
			};
			modernActionButton2.Click += delegate
			{
				LaunchOfficialDeviceSelector();
			};
			panel4.Controls.Add(modernActionButton2);
			chkMasterBypass = new DolphinXVDO.ModernBypassButton
			{
				Location = new System.Drawing.Point(174, 11),
				Size = new System.Drawing.Size(88, 30)
			};
			chkMasterBypass.CheckedChanged += delegate
			{
				masterBypass = chkMasterBypass.Checked;
				pnlGraph.Invalidate();
				TriggerSync(true);
			};
			panel4.Controls.Add(chkMasterBypass);
			DolphinXVDO.GearIconButton gearIconButton = new DolphinXVDO.GearIconButton
			{
				Location = new System.Drawing.Point(270, 11),
				Size = new System.Drawing.Size(30, 30)
			};
			gearIconButton.Click += delegate
			{
				OpenSettingsDialog();
			};
			panel4.Controls.Add(gearIconButton);
			DolphinXVDO.WindowControlButton windowControlButton = new DolphinXVDO.WindowControlButton(DolphinXVDO.WindowControlButton.ButtonType.Minimize);
			windowControlButton.Location = new System.Drawing.Point(316, 19);
			windowControlButton.Click += delegate
			{
				base.WindowState = System.Windows.Forms.FormWindowState.Minimized;
			};
			panel4.Controls.Add(windowControlButton);
			DolphinXVDO.WindowControlButton windowControlButton2 = new DolphinXVDO.WindowControlButton(DolphinXVDO.WindowControlButton.ButtonType.Maximize);
			windowControlButton2.Location = new System.Drawing.Point(336, 19);
			windowControlButton2.Click += delegate
			{
				if (base.WindowState == System.Windows.Forms.FormWindowState.Maximized)
				{
					base.WindowState = System.Windows.Forms.FormWindowState.Normal;
				}
				else
				{
					base.WindowState = System.Windows.Forms.FormWindowState.Maximized;
				}
			};
			panel4.Controls.Add(windowControlButton2);
			DolphinXVDO.WindowControlButton windowControlButton3 = new DolphinXVDO.WindowControlButton(DolphinXVDO.WindowControlButton.ButtonType.Close);
			windowControlButton3.Location = new System.Drawing.Point(356, 19);
			windowControlButton3.Click += delegate
			{
				Close();
			};
			panel4.Controls.Add(windowControlButton3);
			System.Windows.Forms.ToolTip winBtnTip = new System.Windows.Forms.ToolTip();
			winBtnTip.SetToolTip(windowControlButton, "Minimize");
			winBtnTip.SetToolTip(windowControlButton2, "Maximize");
			winBtnTip.SetToolTip(windowControlButton3, "Close to System Tray");
			pnlTitleBar.Controls.Add(panel4);

			DoubleBufferedPanel pnlFooter = new DoubleBufferedPanel
			{
				Dock = DockStyle.Bottom,
				Height = 36,
				BackColor = BG_CARD,
				Padding = new Padding(12, 5, 12, 5)
			};
			pnlFooter.Paint += delegate(object s, PaintEventArgs e)
			{
				using (Pen pen = new Pen(UiTheme.BorderSubtle, 1f))
				{
					e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
				}
			};

			btnVstRack = new ModernActionButton
			{
				Text = "VST Rack",
				Dock = DockStyle.Right,
				Width = 96,
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				PressedBackColor = UiTheme.BgPressed,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.TextBody,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.DisplayFont(8.4f, FontStyle.Bold)
			};
			btnVstRack.Click += delegate
			{
				OpenVstRackDialog();
			};
			Panel pnlFooterGap = new Panel
			{
				Dock = DockStyle.Right,
				Width = 10,
				BackColor = Color.Transparent
			};
			lblStatusUsage = new Label
			{
				Dock = DockStyle.Fill,
				AutoSize = false,
				AutoEllipsis = true,
				BackColor = Color.Transparent,
				TextAlign = ContentAlignment.MiddleLeft,
				Font = UiTheme.MonoFont(7.8f, FontStyle.Regular),
				ForeColor = UiTheme.TextMuted,
				Text = "● Estimated: Peak: 0.0 dB  |  Latency: 0.0 ms (0 s.)  |  Init: 1.3 ms  |  CPU: 1.2 % (one core)"
			};
			pnlFooter.Controls.Add(lblStatusUsage);
			pnlFooter.Controls.Add(pnlFooterGap);
			pnlFooter.Controls.Add(btnVstRack);

			base.Controls.Add(pnlFooter);

			System.Windows.Forms.Panel panel6 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = BG_MAIN,
				Padding = new System.Windows.Forms.Padding(10, 8, 10, 8)
			};
			base.Controls.Add(panel6);
			panel6.BringToFront();
			System.Windows.Forms.Panel panel8 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Left,
				Width = 272,
				BackColor = BG_MAIN,
				Padding = new System.Windows.Forms.Padding(0, 0, 8, 0)
			};
			panel6.Controls.Add(panel8);
			System.Windows.Forms.Panel panel9 = CreateCard("Presets", 154);
			panel9.Dock = System.Windows.Forms.DockStyle.Top;
			System.Windows.Forms.Panel panel11 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.Transparent,
				Padding = new System.Windows.Forms.Padding(12, 2, 12, 12)
			};
			panel9.Controls.Add(panel11);
			panel11.BringToFront();
			cbPresets = new DolphinXVDO.ModernDropDownList
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 30
			};
			cbPresets.SelectedIndexChanged += delegate
			{
				if (!isInitializing && cbPresets.SelectedItem != null)
				{
					ApplyPreset(cbPresets.SelectedItem.ToString());
				}
			};
			panel11.Controls.Add(cbPresets);
			System.Windows.Forms.Panel panel13 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Bottom,
				Height = 62,
				BackColor = System.Drawing.Color.Transparent
			};
			DolphinXVDO.ModernActionButton modernActionButton4 = new DolphinXVDO.ModernActionButton
			{
				Text = "Save",
				Location = new System.Drawing.Point(0, 0),
				Size = new System.Drawing.Size(114, 28),
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				PressedBackColor = UiTheme.BgPressed,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.TextBody,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.DisplayFont(8.8f, System.Drawing.FontStyle.Bold)
			};
			modernActionButton4.Click += delegate
			{
				PromptSavePreset();
			};
			DolphinXVDO.ModernActionButton modernActionButton6 = new DolphinXVDO.ModernActionButton
			{
				Text = "Delete",
				Location = new System.Drawing.Point(122, 0),
				Size = new System.Drawing.Size(114, 28),
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				PressedBackColor = UiTheme.BgPressed,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentAlert,
				TextColor = UiTheme.TextMuted,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.DisplayFont(8.8f, System.Drawing.FontStyle.Bold)
			};
			modernActionButton6.Click += delegate
			{
				DeleteCurrentPreset();
			};
			DolphinXVDO.ModernActionButton btnExport = new DolphinXVDO.ModernActionButton
			{
				Text = "Export",
				Location = new System.Drawing.Point(0, 34),
				Size = new System.Drawing.Size(114, 28),
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				PressedBackColor = UiTheme.BgPressed,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.AccentBlue,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.DisplayFont(8.8f, System.Drawing.FontStyle.Bold)
			};
			btnExport.Click += delegate
			{
				ExportCurrentPreset();
			};
			DolphinXVDO.ModernActionButton btnImport = new DolphinXVDO.ModernActionButton
			{
				Text = "Import",
				Location = new System.Drawing.Point(122, 34),
				Size = new System.Drawing.Size(114, 28),
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				PressedBackColor = UiTheme.BgPressed,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentGreen,
				TextColor = UiTheme.AccentGreen,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.DisplayFont(8.8f, System.Drawing.FontStyle.Bold)
			};
			btnImport.Click += delegate
			{
				ImportPreset();
			};
			panel13.Controls.Add(modernActionButton4);
			panel13.Controls.Add(modernActionButton6);
			panel13.Controls.Add(btnExport);
			panel13.Controls.Add(btnImport);
			panel11.Controls.Add(panel13);
			System.Windows.Forms.Panel panel15 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 8,
				BackColor = BG_MAIN
			};
			System.Windows.Forms.Panel panel16 = CreateCard("ReaComp", 198);
			panel16.Dock = System.Windows.Forms.DockStyle.Top;
			System.Windows.Forms.Panel panel17 = panel16.Controls[0] as System.Windows.Forms.Panel;
			if (panel17 != null)
			{
				System.Windows.Forms.Panel panel19 = new DoubleBufferedPanel
				{
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 42,
					BackColor = System.Drawing.Color.Transparent,
					Padding = new System.Windows.Forms.Padding(4, 3, 10, 3)
				};
				btnCompPower = new DolphinXVDO.ApoPowerButton
				{
					Dock = System.Windows.Forms.DockStyle.Fill,
					BackColor = System.Drawing.Color.Transparent,
					Checked = chkCompPower.Checked
				};
				btnCompPower.CheckedChanged += delegate
				{
					chkCompPower.Checked = btnCompPower.Checked;
					TriggerSync(true);
				};
				panel19.Controls.Add(btnCompPower);
				panel17.Controls.Add(panel19);
				panel19.SendToBack();
				System.Windows.Forms.Label label3 = panel17.Controls.OfType<System.Windows.Forms.Label>().FirstOrDefault();
				if (label3 != null)
				{
					label3.BringToFront();
				}
			}
			System.Windows.Forms.Panel panel21 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.Transparent,
				Padding = new System.Windows.Forms.Padding(12, 2, 12, 12)
			};
			panel16.Controls.Add(panel21);
			panel21.BringToFront();
			DoubleBufferedPanel pnlThreshBox = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 96,
				BackColor = System.Drawing.Color.Transparent,
				Padding = new System.Windows.Forms.Padding(12, 10, 12, 10)
			};
			pnlThreshBox.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, pnlThreshBox.Width, pnlThreshBox.Height, 7f, UiTheme.BgWell, UiTheme.BorderRing, 1f);
			};
			DoubleBufferedPanel pnlThreshInner = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.Transparent
			};
			System.Windows.Forms.Label lblThreshTitle = new System.Windows.Forms.Label
			{
				Text = "Threshold",
				ForeColor = UiTheme.TextBody,
				BackColor = System.Drawing.Color.Transparent,
				Font = UiTheme.DisplayFont(9f, System.Drawing.FontStyle.Bold),
				TextAlign = System.Drawing.ContentAlignment.MiddleLeft
			};
			bool updatingThresh = false;
			sliderThresh = new DolphinXVDO.ModernHorizontalSlider
			{
				MinValue = -60.0,
				MaxValue = 0.0,
				Value = -22.0,
				DecimalPlaces = 1
			};
			sliderThresh.ValueChanged += delegate
			{
				if (!updatingThresh)
				{
					updatingThresh = true;
					txtThreshVal.Text = sliderThresh.Value.ToString("0.0");
					updatingThresh = false;
					TriggerSync();
				}
			};
			DoubleBufferedPanel pnlThreshValGroup = new DoubleBufferedPanel
			{
				Size = new System.Drawing.Size(112, 26),
				BackColor = System.Drawing.Color.Transparent
			};
			txtThreshVal = new System.Windows.Forms.TextBox
			{
				Text = "-22.0",
				ForeColor = UiTheme.AccentBlue,
				Font = UiTheme.MonoFont(9.6f, System.Drawing.FontStyle.Bold),
				TextAlign = System.Windows.Forms.HorizontalAlignment.Center
			};
			DolphinXVDO.ModernNumberBox value4 = new DolphinXVDO.ModernNumberBox(txtThreshVal, 6)
			{
				Location = new System.Drawing.Point(0, 0),
				Size = new System.Drawing.Size(80, 26)
			};
			System.Windows.Forms.Label value5 = new System.Windows.Forms.Label
			{
				Text = "dB",
				ForeColor = UiTheme.TextMuted,
				BackColor = System.Drawing.Color.Transparent,
				Font = UiTheme.MonoFont(8.5f, System.Drawing.FontStyle.Regular),
				Location = new System.Drawing.Point(86, 0),
				Size = new System.Drawing.Size(26, 26),
				TextAlign = System.Drawing.ContentAlignment.MiddleLeft
			};
			pnlThreshValGroup.Controls.Add(value4);
			pnlThreshValGroup.Controls.Add(value5);
			pnlThreshInner.Controls.Add(lblThreshTitle);
			pnlThreshInner.Controls.Add(sliderThresh);
			pnlThreshInner.Controls.Add(pnlThreshValGroup);
			Action layoutThreshInner = delegate
			{
				int iw = pnlThreshInner.ClientSize.Width;
				if (iw <= 0) return;
				lblThreshTitle.SetBounds(4, 0, Math.Max(40, iw - 8), 18);
				sliderThresh.SetBounds(0, 22, iw, 24);
				// Center based on visual width (80px box + 6px gap + 18px "dB" glyph = 104px)
				int gx = Math.Max(0, (iw - 104) / 2);
				pnlThreshValGroup.SetBounds(gx, 50, pnlThreshValGroup.Width, 26);
			};
			pnlThreshInner.Layout += delegate
			{
				layoutThreshInner();
			};
			pnlThreshInner.Resize += delegate
			{
				layoutThreshInner();
			};
			System.Action commitThresh = delegate
			{
				double result3;
				if (double.TryParse(txtThreshVal.Text, out result3))
				{
					result3 = System.Math.Max(-60.0, System.Math.Min(0.0, result3));
					updatingThresh = true;
					sliderThresh.Value = result3;
					txtThreshVal.Text = result3.ToString("0.0");
					updatingThresh = false;
					TriggerSync(true);
				}
			};
			txtThreshVal.KeyDown += delegate(object s, System.Windows.Forms.KeyEventArgs e)
			{
				if (e.KeyCode == System.Windows.Forms.Keys.Return)
				{
					commitThresh();
					e.SuppressKeyPress = true;
				}
			};
			txtThreshVal.Leave += delegate
			{
				commitThresh();
			};
			pnlThreshBox.Controls.Add(pnlThreshInner);
			System.Windows.Forms.Panel value6 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 10,
				BackColor = System.Drawing.Color.Transparent
			};
			DolphinXVDO.ModernActionButton modernActionButton8 = new DolphinXVDO.ModernActionButton
			{
				Text = "ReaComp Advance Setting",
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 34,
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				PressedBackColor = UiTheme.BgPressed,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.AccentBlue,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.DisplayFont(9f, System.Drawing.FontStyle.Bold)
			};
			modernActionButton8.Click += delegate
			{
				using (DolphinXVDO.AdvancedCompDialog advancedCompDialog = new DolphinXVDO.AdvancedCompDialog(this))
				{
					if (advancedCompDialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
					{
						TriggerSync(true);
					}
				}
			};
			panel21.Controls.Add(modernActionButton8);
			panel21.Controls.Add(value6);
			panel21.Controls.Add(pnlThreshBox);
			System.Windows.Forms.Panel panel28 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 8,
				BackColor = BG_MAIN
			};
			System.Windows.Forms.Panel panel29 = CreateCard("Output Level (Stereo VU)", 130);
			panel29.Dock = System.Windows.Forms.DockStyle.Top;
			System.Windows.Forms.Panel panel31 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.Transparent,
				Padding = new System.Windows.Forms.Padding(8, 2, 8, 6)
			};
			vuMeter = new DolphinXVDO.ModernVuMeterControl();
			panel31.Controls.Add(vuMeter);
			panel29.Controls.Add(panel31);
			panel31.BringToFront();
			System.Windows.Forms.Panel panelEstSpacer = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 8,
				BackColor = BG_MAIN
			};
			System.Windows.Forms.Panel pnlEstCard = CreateEstimatedPropertiesCard();
			pnlEstCard.Dock = System.Windows.Forms.DockStyle.Top;
			pnlEstCard.Height = 114;
			panel8.Controls.Add(pnlEstCard);
			panel8.Controls.Add(panelEstSpacer);
			panel8.Controls.Add(panel29);
			panel8.Controls.Add(panel28);
			panel8.Controls.Add(panel16);
			panel8.Controls.Add(panel15);
			panel8.Controls.Add(panel9);
			panel9.BringToFront();
			panel15.BringToFront();
			panel16.BringToFront();
			panel28.BringToFront();
			panel29.BringToFront();
			panelEstSpacer.BringToFront();
			pnlEstCard.BringToFront();
			System.Windows.Forms.Panel panel33 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = BG_MAIN
			};
			panel6.Controls.Add(panel33);
			panel8.SendToBack();
			panel33.BringToFront();
			DoubleBufferedPanel panel35 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 166,
				BackColor = BG_MAIN,
				Padding = new System.Windows.Forms.Padding(2)
			};
			panel35.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, panel35.Width, panel35.Height, 8f, UiTheme.BgCard, UiTheme.BorderRing, 1f);
			};
			pnlGraph = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.Transparent
			};
			pnlGraph.Paint += new System.Windows.Forms.PaintEventHandler(DrawFrequencyResponseGraph);
			panel35.Controls.Add(pnlGraph);
			pnlGraph.BringToFront();
			panel33.Controls.Add(panel35);
			System.Windows.Forms.Panel pnlGraphGap = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 6,
				BackColor = BG_MAIN
			};
			DoubleBufferedPanel panel37 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 42,
				BackColor = BG_MAIN,
				Padding = new System.Windows.Forms.Padding(12, 7, 12, 7)
			};
			panel37.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, panel37.Width, panel37.Height, 8f, UiTheme.BgCard, UiTheme.BorderRing, 1f);
			};
			System.Windows.Forms.Label value7 = new System.Windows.Forms.Label
			{
				Text = "PREAMP GAIN",
				ForeColor = UiTheme.TextMuted,
				BackColor = System.Drawing.Color.Transparent,
				Font = UiTheme.MonoFont(7.8f, System.Drawing.FontStyle.Bold),
				Dock = System.Windows.Forms.DockStyle.Left,
				Width = 88,
				TextAlign = System.Drawing.ContentAlignment.MiddleLeft
			};
			panel37.Controls.Add(value7);
			System.Windows.Forms.Label value8 = new System.Windows.Forms.Label
			{
				Text = "dB (\u00B130)",
				ForeColor = UiTheme.TextMuted,
				BackColor = System.Drawing.Color.Transparent,
				Font = UiTheme.MonoFont(7.2f),
				Dock = System.Windows.Forms.DockStyle.Right,
				Width = 60,
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter
			};
			panel37.Controls.Add(value8);
			System.Windows.Forms.Panel pnlPreGap2 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Right,
				Width = 5,
				BackColor = System.Drawing.Color.Transparent
			};
			panel37.Controls.Add(pnlPreGap2);
			txtPreampVal = new System.Windows.Forms.TextBox
			{
				Text = "0.0",
				ForeColor = UiTheme.TextPrimary,
				Font = UiTheme.MonoFont(9f, System.Drawing.FontStyle.Bold),
				TextAlign = System.Windows.Forms.HorizontalAlignment.Center
			};
			DolphinXVDO.ModernNumberBox value9 = new DolphinXVDO.ModernNumberBox(txtPreampVal, 6)
			{
				Dock = System.Windows.Forms.DockStyle.Right,
				Width = 56
			};
			panel37.Controls.Add(value9);
			System.Windows.Forms.Panel pnlPreGap1 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Right,
				Width = 6,
				BackColor = System.Drawing.Color.Transparent
			};
			panel37.Controls.Add(pnlPreGap1);
			btnAutoPreamp = new DolphinXVDO.ModernActionButton
			{
				Text = "Auto Preamp",
				VectorIcon = "bolt",
				Dock = System.Windows.Forms.DockStyle.Right,
				Width = 114,
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				PressedBackColor = UiTheme.BgPressed,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.TextBody,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.MonoFont(7.4f, System.Drawing.FontStyle.Bold)
			};
			btnAutoPreamp.Click += delegate
			{
				ApplyAutoPreamp();
			};
			panel37.Controls.Add(btnAutoPreamp);
			sliderPreamp = new DolphinXVDO.ModernHorizontalSlider
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				MinValue = -30.0,
				MaxValue = 30.0,
				Value = 0.0
			};
			bool updatingPre = false;
			sliderPreamp.ValueChanged += delegate
			{
				if (!updatingPre)
				{
					updatingPre = true;
					preampDb = sliderPreamp.Value;
					txtPreampVal.Text = string.Format("{0:+0.0;-0.0;0.0}", preampDb);
					txtPreampVal.ForeColor = ((preampDb > 0.0) ? UiTheme.AccentGreen : ((preampDb < 0.0) ? UiTheme.AccentBlue : UiTheme.TextPrimary));
					pnlGraph.Invalidate();
					TriggerSync();
					updatingPre = false;
				}
			};
			txtPreampVal.KeyDown += delegate(object s, System.Windows.Forms.KeyEventArgs e)
			{
				if (e.KeyCode == System.Windows.Forms.Keys.Return)
				{
					double result2;
					if (double.TryParse(txtPreampVal.Text, out result2))
					{
						updatingPre = true;
						sliderPreamp.Value = result2;
						preampDb = sliderPreamp.Value;
						txtPreampVal.ForeColor = ((preampDb > 0.0) ? UiTheme.AccentGreen : ((preampDb < 0.0) ? UiTheme.AccentBlue : UiTheme.TextPrimary));
						pnlGraph.Invalidate();
						TriggerSync(true);
						updatingPre = false;
					}
					e.SuppressKeyPress = true;
				}
			};
			txtPreampVal.Leave += delegate
			{
				double result;
				if (double.TryParse(txtPreampVal.Text, out result))
				{
					updatingPre = true;
					sliderPreamp.Value = result;
					preampDb = sliderPreamp.Value;
					txtPreampVal.ForeColor = ((preampDb > 0.0) ? UiTheme.AccentGreen : ((preampDb < 0.0) ? UiTheme.AccentBlue : UiTheme.TextPrimary));
					pnlGraph.Invalidate();
					TriggerSync(true);
					updatingPre = false;
				}
			};
			panel37.Controls.Add(sliderPreamp);
			sliderPreamp.BringToFront();
			System.Windows.Forms.Panel panel39 = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 6,
				BackColor = BG_MAIN
			};
			System.Windows.Forms.Panel panel40 = CreateCard("15-Band Parametric Equalizer", 0);
			panel40.Dock = System.Windows.Forms.DockStyle.Fill;
			System.Windows.Forms.Panel panel41 = panel40.Controls[0] as System.Windows.Forms.Panel;
			if (panel41 != null)
			{
				panel41.Height = 38;
				lblEqTitle = panel41.Controls.OfType<System.Windows.Forms.Label>().FirstOrDefault();
				System.Windows.Forms.Panel panel43 = new DoubleBufferedPanel
				{
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 194,
					Height = 38,
					BackColor = System.Drawing.Color.Transparent,
					Padding = new System.Windows.Forms.Padding(2, 5, 10, 5)
				};
				System.Windows.Forms.Panel panel45 = new DoubleBufferedPanel
				{
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 28,
					BackColor = System.Drawing.Color.Transparent
				};
				btnEqPower = new DolphinXVDO.ApoPowerButton
				{
					Dock = System.Windows.Forms.DockStyle.Fill,
					BackColor = System.Drawing.Color.Transparent,
					Checked = false
				};
				btnEqPower.CheckedChanged += delegate
				{
					pnlGraph.Invalidate();
					TriggerSync(true);
				};
				panel45.Controls.Add(btnEqPower);
				System.Windows.Forms.Panel value10 = new System.Windows.Forms.Panel
				{
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 8,
					BackColor = System.Drawing.Color.Transparent
				};
				DolphinXVDO.ModernActionButton modernActionButton10 = new DolphinXVDO.ModernActionButton
				{
					Text = "Reset EQ",
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 72,
					CornerRadius = 6,
					NormalBackColor = UiTheme.BgWell,
					HoverBackColor = UiTheme.BgChip,
					PressedBackColor = UiTheme.BgPressed,
					NormalBorderColor = UiTheme.BorderRing,
					HoverBorderColor = UiTheme.AccentBlue,
					TextColor = UiTheme.TextBody,
					HoverTextColor = UiTheme.TextPrimary,
					Font = UiTheme.DisplayFont(8f, System.Drawing.FontStyle.Bold)
				};
				modernActionButton10.Click += delegate
				{
					foreach (DolphinXVDO.BiquadFilter filter in filters)
					{
						filter.Gain = 0.0;
						filter.CalculateCoefficients();
					}
					RebuildBandUI();
					pnlGraph.Invalidate();
					TriggerSync(true);
				};
				System.Windows.Forms.Panel value11 = new System.Windows.Forms.Panel
				{
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 6,
					BackColor = System.Drawing.Color.Transparent
				};
				btnAddBand = new DolphinXVDO.ModernActionButton
				{
					Text = "+",
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 28,
					CornerRadius = 6,
					NormalBackColor = UiTheme.BgWell,
					HoverBackColor = UiTheme.BgChip,
					PressedBackColor = UiTheme.BgPressed,
					NormalBorderColor = UiTheme.BorderRing,
					HoverBorderColor = UiTheme.AccentBlue,
					TextColor = UiTheme.AccentBlue,
					HoverTextColor = UiTheme.TextPrimary,
					Font = UiTheme.MonoFont(10f, System.Drawing.FontStyle.Bold)
				};
				btnAddBand.Click += delegate
				{
					AddBand();
				};
				System.Windows.Forms.Panel value12 = new System.Windows.Forms.Panel
				{
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 4,
					BackColor = System.Drawing.Color.Transparent
				};
				btnRemoveBand = new DolphinXVDO.ModernActionButton
				{
					Text = "-",
					Dock = System.Windows.Forms.DockStyle.Right,
					Width = 28,
					CornerRadius = 6,
					NormalBackColor = UiTheme.BgWell,
					HoverBackColor = UiTheme.BgChip,
					PressedBackColor = UiTheme.BgPressed,
					NormalBorderColor = UiTheme.BorderRing,
					HoverBorderColor = UiTheme.AccentAlert,
					TextColor = UiTheme.TextMuted,
					HoverTextColor = UiTheme.TextPrimary,
					Font = UiTheme.MonoFont(10f, System.Drawing.FontStyle.Bold)
				};
				btnRemoveBand.Click += delegate
				{
					RemoveBand();
				};
				panel43.Controls.Add(btnAddBand);
				panel43.Controls.Add(value12);
				panel43.Controls.Add(btnRemoveBand);
				panel43.Controls.Add(value11);
				panel43.Controls.Add(modernActionButton10);
				panel43.Controls.Add(value10);
				panel43.Controls.Add(panel45);
				panel41.Controls.Add(panel43);
				panel43.SendToBack();
				if (lblEqTitle != null)
				{
					lblEqTitle.BringToFront();
				}
			}
			System.Windows.Forms.Panel panel50 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.Transparent,
				Padding = new System.Windows.Forms.Padding(8, 4, 8, 8)
			};
			panel40.Controls.Add(panel50);
			panel50.BringToFront();
			System.Windows.Forms.Panel panel52 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Left,
				Width = 44,
				Margin = new System.Windows.Forms.Padding(0),
				Padding = new System.Windows.Forms.Padding(1),
				BackColor = System.Drawing.Color.Transparent
			};
			System.Windows.Forms.Panel panel54 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 50,
				Padding = new System.Windows.Forms.Padding(1, 0, 1, 2),
				BackColor = System.Drawing.Color.Transparent
			};
			DoubleBufferedPanel pnlFilterLbl = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 20,
				BackColor = System.Drawing.Color.Transparent
			};
			pnlFilterLbl.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, pnlFilterLbl.Width, pnlFilterLbl.Height, 5f, UiTheme.BgChip, UiTheme.BorderSubtle, 1f);
				e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				using (Font f = UiTheme.MonoFont(6.5f, FontStyle.Bold))
				using (SolidBrush br = new SolidBrush(UiTheme.TextMuted))
				using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
				{
					e.Graphics.DrawString("FILTER", f, br, new RectangleF(0f, 0f, pnlFilterLbl.Width, pnlFilterLbl.Height), sf);
				}
			};
			System.Windows.Forms.Label value14 = new System.Windows.Forms.Label
			{
				Text = "Hz",
				Dock = System.Windows.Forms.DockStyle.Bottom,
				Height = 24,
				BackColor = System.Drawing.Color.Transparent,
				ForeColor = UiTheme.TextMuted,
				Font = UiTheme.MonoFont(7.5f, System.Drawing.FontStyle.Bold),
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter
			};
			panel54.Controls.Add(value14);
			panel54.Controls.Add(pnlFilterLbl);
			panel52.Controls.Add(panel54);
			System.Windows.Forms.Panel panel56 = new DoubleBufferedPanel
			{
				Dock = System.Windows.Forms.DockStyle.Bottom,
				Height = 54,
				Padding = new System.Windows.Forms.Padding(1, 2, 1, 0),
				BackColor = System.Drawing.Color.Transparent
			};
			System.Windows.Forms.Label value15 = new System.Windows.Forms.Label
			{
				Text = "Gain",
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 24,
				BackColor = System.Drawing.Color.Transparent,
				ForeColor = UiTheme.AccentBlue,
				Font = UiTheme.MonoFont(7.2f, System.Drawing.FontStyle.Bold),
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter
			};
			System.Windows.Forms.Label value16 = new System.Windows.Forms.Label
			{
				Text = "Q",
				Dock = System.Windows.Forms.DockStyle.Bottom,
				Height = 24,
				BackColor = System.Drawing.Color.Transparent,
				ForeColor = UiTheme.TextMuted,
				Font = UiTheme.MonoFont(7.5f, System.Drawing.FontStyle.Bold),
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter
			};
			panel56.Controls.Add(value15);
			panel56.Controls.Add(value16);
			panel52.Controls.Add(panel56);
			DolphinXVDO.EqRulerControl eqRulerControl2 = new DolphinXVDO.EqRulerControl
			{
				Dock = System.Windows.Forms.DockStyle.Fill
			};
			panel52.Controls.Add(eqRulerControl2);
			eqRulerControl2.BringToFront();
			tlpBands = new System.Windows.Forms.TableLayoutPanel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.Transparent,
				RowCount = 1
			};
			tlpBands.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
			panel50.Controls.Add(panel52);
			panel50.Controls.Add(tlpBands);
			tlpBands.BringToFront();
			RebuildBandUI();
			panel33.Controls.Add(panel40);
			panel33.Controls.Add(panel39);
			panel33.Controls.Add(panel37);
			panel33.Controls.Add(pnlGraphGap);
			panel33.Controls.Add(panel35);
			panel35.BringToFront();
			pnlGraphGap.BringToFront();
			panel37.BringToFront();
			panel39.BringToFront();
			panel40.BringToFront();
			InitWasapiMeter();
			vuTimer = new System.Windows.Forms.Timer
			{
				Interval = 30
			};
			vuTimer.Tick += new System.EventHandler(VuTimer_Tick);
			vuTimer.Start();
		}

		private void InitWasapiMeter()
		{
			try
			{
				IMMDeviceEnumerator iMMDeviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
				IMMDevice ppDevice;
				if (iMMDeviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out ppDevice) != 0 || ppDevice == null)
				{
					return;
				}
				currentAudioDevice = ppDevice;
				Guid iid = typeof(IAudioMeterInformation).GUID;
				object ppInterface;
				if (ppDevice.Activate(ref iid, 1, IntPtr.Zero, out ppInterface) == 0 && ppInterface != null)
				{
					wasapiMeter = (IAudioMeterInformation)ppInterface;
					wasapiMeter.GetMeteringChannelCount(out meterChannelCount);
					if (meterChannelCount <= 0)
					{
						meterChannelCount = 2;
					}
					if (pMeterChannelsMem != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(pMeterChannelsMem);
					}
					pMeterChannelsMem = Marshal.AllocHGlobal(meterChannelCount * 4);
				}
			}
			catch
			{
			}
		}

		private void VuTimer_Tick(object sender, EventArgs e)
		{
			if (base.WindowState == FormWindowState.Minimized || !base.Visible || vuMeter == null)
			{
				return;
			}
			try
			{
				if (wasapiMeter != null)
				{
					goto IL_0060;
				}
				InitWasapiMeter();
				if (wasapiMeter != null)
				{
					goto IL_0060;
				}
				goto end_IL_002d;
				IL_0060:
				float rawL = 0f;
				float rawR = 0f;
				float pfPeak;
				if (pMeterChannelsMem != IntPtr.Zero && meterChannelCount >= 1)
				{
					if (wasapiMeter.GetChannelsPeakValues(meterChannelCount, pMeterChannelsMem) == 0)
					{
						if (cachedMeterBuffer == null || cachedMeterBuffer.Length < meterChannelCount)
						{
							cachedMeterBuffer = new float[meterChannelCount];
						}
						Marshal.Copy(pMeterChannelsMem, cachedMeterBuffer, 0, meterChannelCount);
						rawL = cachedMeterBuffer[0];
						rawR = ((meterChannelCount > 1) ? cachedMeterBuffer[1] : cachedMeterBuffer[0]);
					}
					else if (wasapiMeter.GetPeakValue(out pfPeak) == 0)
					{
						rawL = pfPeak;
						rawR = pfPeak;
					}
				}
				else if (wasapiMeter.GetPeakValue(out pfPeak) == 0)
				{
					rawL = pfPeak;
					rawR = pfPeak;
				}
				vuMeter.UpdateLevels(rawL, rawR);
				end_IL_002d:;
			}
			catch
			{
				wasapiMeter = null;
			}
		}

		private void AddBand()
		{
			if (filters.Count < 15)
			{
				int count = filters.Count;
				double fc = ((count >= Extended15Frequencies.Length) ? Math.Min(20000.0, filters[filters.Count - 1].Frequency * 1.4) : Extended15Frequencies[count]);
				filters.Add(new BiquadFilter("PK", fc, 0.0, 1.41));
				RebuildBandUI();
			}
		}

		private void RemoveBand()
		{
			if (filters.Count > 1)
			{
				filters.RemoveAt(filters.Count - 1);
				RebuildBandUI();
			}
		}

		private static void DisposeControlTree(Control root)
		{
			if (root == null) return;
			while (root.Controls.Count > 0)
			{
				Control child = root.Controls[0];
				root.Controls.RemoveAt(0);
				DisposeControlTree(child);
			}
			root.Dispose();
		}

		private void RebuildBandUI()
		{
			if (tlpBands == null)
			{
				return;
			}
			isRebuildingBands = true;
			try
			{
				tlpBands.SuspendLayout();
				while (tlpBands.Controls.Count > 0)
				{
					Control ctrl = tlpBands.Controls[0];
					tlpBands.Controls.RemoveAt(0);
					DisposeControlTree(ctrl);
				}
				tlpBands.ColumnStyles.Clear();
				bandSliders.Clear();
				bandGainBoxes.Clear();
				bandFreqBoxes.Clear();
				bandQBoxes.Clear();
				int count = filters.Count;
				tlpBands.ColumnCount = count;
				for (int i = 0; i < count; i++)
				{
					tlpBands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / (float)count));
				}
				for (int j = 0; j < count; j++)
				{
					int index = j;
					DoubleBufferedPanel panel2 = new DoubleBufferedPanel
					{
						Dock = DockStyle.Fill,
						BackColor = Color.Transparent,
						Margin = new Padding(1, 0, 1, 0),
						Padding = new Padding(0)
					};
					DoubleBufferedPanel panel4 = new DoubleBufferedPanel
					{
						Dock = DockStyle.Top,
						Height = 50,
						BackColor = Color.Transparent,
						Padding = new Padding(1, 0, 1, 2)
					};
					FilterTypeButton btnType = new FilterTypeButton(filters[index].Type)
					{
						Dock = DockStyle.Top,
						Height = 20
					};
					btnType.FilterTypeChanged += delegate
					{
						if (isRebuildingBands || index < 0 || index >= filters.Count) return;
						filters[index].Type = btnType.FilterType;
						filters[index].CalculateCoefficients();
						pnlGraph.Invalidate();
						TriggerSync(true);
					};
					panel4.Controls.Add(btnType);
					TextBox txtFreq = new TextBox
					{
						Text = ((int)filters[index].Frequency).ToString(),
						ForeColor = UiTheme.TextBody,
						Font = UiTheme.MonoFont((count > 12) ? 6.6f : 7.5f, FontStyle.Bold),
						TextAlign = HorizontalAlignment.Center
					};
					ModernNumberBox value = new ModernNumberBox(txtFreq, 5)
					{
						Dock = DockStyle.Bottom,
						Height = 24
					};
					Action commitFreq = delegate
					{
						if (isRebuildingBands || index < 0 || index >= filters.Count) return;
						double result3;
						if (double.TryParse(txtFreq.Text, out result3) && result3 >= 20.0 && result3 <= 22000.0)
						{
							filters[index].Frequency = result3;
							filters[index].CalculateCoefficients();
							pnlGraph.Invalidate();
							TriggerSync(true);
						}
					};
					txtFreq.KeyDown += delegate(object s, KeyEventArgs e)
					{
						if (e.KeyCode == Keys.Return)
						{
							commitFreq();
							e.SuppressKeyPress = true;
						}
					};
					txtFreq.Leave += delegate
					{
						commitFreq();
					};
					bandFreqBoxes.Add(txtFreq);
					panel4.Controls.Add(value);
					panel2.Controls.Add(panel4);
					DoubleBufferedPanel panel6 = new DoubleBufferedPanel
					{
						Dock = DockStyle.Bottom,
						Height = 54,
						BackColor = Color.Transparent,
						Padding = new Padding(1, 2, 1, 0)
					};
					TextBox txtG = new TextBox
					{
						Text = filters[index].Gain.ToString("0.0"),
						ForeColor = ((filters[index].Gain > 0.0) ? UiTheme.AccentGreen : ((filters[index].Gain < 0.0) ? UiTheme.AccentBlue : UiTheme.TextMuted)),
						Font = UiTheme.MonoFont((count > 12) ? 6.6f : 7.5f, FontStyle.Bold),
						TextAlign = HorizontalAlignment.Center
					};
					ModernNumberBox value2 = new ModernNumberBox(txtG, 5)
					{
						Dock = DockStyle.Top,
						Height = 24
					};
					bandGainBoxes.Add(txtG);
					panel6.Controls.Add(value2);
					TextBox txtQ = new TextBox
					{
						Text = filters[index].Q.ToString("0.00"),
						ForeColor = UiTheme.TextMuted,
						Font = UiTheme.MonoFont((count > 12) ? 6.5f : 7.3f, FontStyle.Regular),
						TextAlign = HorizontalAlignment.Center
					};
					ModernNumberBox value3 = new ModernNumberBox(txtQ, 5)
					{
						Dock = DockStyle.Bottom,
						Height = 24
					};
					bandQBoxes.Add(txtQ);
					Action commitQ = delegate
					{
						if (isRebuildingBands || index < 0 || index >= filters.Count) return;
						double result2;
						if (double.TryParse(txtQ.Text, out result2) && result2 > 0.05 && result2 <= 20.0)
						{
							filters[index].Q = result2;
							filters[index].CalculateCoefficients();
							pnlGraph.Invalidate();
							TriggerSync(true);
						}
					};
					txtQ.KeyDown += delegate(object s, KeyEventArgs e)
					{
						if (e.KeyCode == Keys.Return)
						{
							commitQ();
							e.SuppressKeyPress = true;
						}
					};
					txtQ.Leave += delegate
					{
						commitQ();
					};
					panel6.Controls.Add(value3);
					panel2.Controls.Add(panel6);
					ModernVerticalSlider vs = new ModernVerticalSlider
					{
						Dock = DockStyle.Fill,
						MinValue = -30.0,
						MaxValue = 30.0,
						Value = filters[index].Gain
					};
					bool updatingBand = false;
					vs.ValueChanged += delegate
					{
						if (isRebuildingBands || index < 0 || index >= filters.Count) return;
						if (!updatingBand)
						{
							updatingBand = true;
							filters[index].Gain = vs.Value;
							filters[index].CalculateCoefficients();
							txtG.Text = vs.Value.ToString("0.0");
							txtG.ForeColor = ((vs.Value > 0.0) ? UiTheme.AccentGreen : ((vs.Value < 0.0) ? UiTheme.AccentBlue : UiTheme.TextMuted));
							pnlGraph.Invalidate();
							TriggerSync();
							updatingBand = false;
						}
					};
					Action commitG = delegate
					{
						if (isRebuildingBands || index < 0 || index >= filters.Count) return;
						double result;
						if (double.TryParse(txtG.Text, out result))
						{
							updatingBand = true;
							vs.Value = result;
							filters[index].Gain = vs.Value;
							filters[index].CalculateCoefficients();
							txtG.ForeColor = ((vs.Value > 0.0) ? UiTheme.AccentGreen : ((vs.Value < 0.0) ? UiTheme.AccentBlue : UiTheme.TextMuted));
							pnlGraph.Invalidate();
							TriggerSync(true);
							updatingBand = false;
						}
					};
					txtG.KeyDown += delegate(object s, KeyEventArgs e)
					{
						if (e.KeyCode == Keys.Return)
						{
							commitG();
							e.SuppressKeyPress = true;
						}
					};
					txtG.Leave += delegate
					{
						commitG();
					};
					bandSliders.Add(vs);
					panel2.Controls.Add(vs);
					vs.BringToFront();
					tlpBands.Controls.Add(panel2, j, 0);
				}
				tlpBands.ResumeLayout(true);
				if (lblEqTitle != null)
				{
					lblEqTitle.Text = "15-Band Parametric Equalizer";
				}
				if (btnAddBand != null)
				{
					btnAddBand.Enabled = count < 15;
				}
				if (btnRemoveBand != null)
				{
					btnRemoveBand.Enabled = count > 1;
				}
				pnlGraph.Invalidate();
				TriggerSync(true);
			}
			finally
			{
				isRebuildingBands = false;
			}
		}

		private Panel CreateCard(string title, int height)
		{
			DoubleBufferedPanel panel2 = new DoubleBufferedPanel
			{
				Dock = DockStyle.Top,
				Height = height,
				BackColor = BG_MAIN,
				Padding = new Padding(2)
			};
			panel2.Paint += delegate(object s, PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, panel2.Width, panel2.Height, 8f, UiTheme.BgCard, UiTheme.BorderRing, 1f);
			};
			DoubleBufferedPanel hdr = new DoubleBufferedPanel
			{
				Dock = DockStyle.Top,
				Height = 34,
				BackColor = Color.Transparent
			};
			Label value = new Label
			{
				Text = title,
				Font = UiTheme.DisplayFont(9.4f, FontStyle.Bold),
				ForeColor = UiTheme.TextPrimary,
				Dock = DockStyle.Fill,
				AutoSize = false,
				BackColor = Color.Transparent,
				Padding = new Padding(12, 0, 0, 0),
				TextAlign = ContentAlignment.MiddleLeft
			};
			hdr.Controls.Add(value);
			panel2.Controls.Add(hdr);
			return panel2;
		}

		private Panel CreateEstimatedPropertiesCard()
		{
			DoubleBufferedPanel card = new DoubleBufferedPanel
			{
				Dock = DockStyle.Top,
				Height = 114,
				BackColor = BG_MAIN,
				Padding = new Padding(2)
			};
			card.Paint += delegate(object s, PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, card.Width, card.Height, 8f, UiTheme.BgCard, UiTheme.BorderRing, 1f);
			};
			DoubleBufferedPanel hdr = new DoubleBufferedPanel
			{
				Dock = DockStyle.Top,
				Height = 26,
				BackColor = Color.Transparent
			};
			Label lblTitle = new Label
			{
				Text = "Estimated properties",
				Font = UiTheme.DisplayFont(8.5f, FontStyle.Bold),
				ForeColor = UiTheme.TextBody,
				Dock = DockStyle.Fill,
				AutoSize = false,
				BackColor = Color.Transparent,
				Padding = new Padding(12, 2, 0, 0),
				TextAlign = ContentAlignment.MiddleLeft
			};
			hdr.Controls.Add(lblTitle);
			card.Controls.Add(hdr);

			DoubleBufferedPanel body = new DoubleBufferedPanel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(10, 2, 10, 8),
				BackColor = Color.Transparent
			};

			DoubleBufferedPanel pnlBox = new DoubleBufferedPanel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(10, 4, 10, 4),
				BackColor = Color.Transparent
			};
			pnlBox.Paint += delegate(object s, PaintEventArgs e)
			{
				UiTheme.DrawRoundedSurface(e.Graphics, pnlBox.Width, pnlBox.Height, 6f, UiTheme.BgWell, UiTheme.BorderRing, 1f);
			};

			TableLayoutPanel tlp = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 2,
				RowCount = 4,
				BackColor = Color.Transparent,
				Padding = new Padding(0)
			};
			tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84f));
			tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			for (int i = 0; i < 4; i++)
			{
				tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
			}

			Font fontKey = UiTheme.MonoFont(7.4f, FontStyle.Regular);
			Font fontVal = UiTheme.MonoFont(7.4f, FontStyle.Bold);
			Color colorKey = UiTheme.TextMuted;
			Color colorVal = UiTheme.TextBody;

			Label k1 = new Label { Text = "Peak gain:", Font = fontKey, ForeColor = colorKey, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
			lblEstPeakGain = new Label { Text = "0.0 dB", Font = fontVal, ForeColor = colorVal, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };

			Label k2 = new Label { Text = "Latency:", Font = fontKey, ForeColor = colorKey, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
			lblEstLatency = new Label { Text = "0.0 ms (0 s.)", Font = fontVal, ForeColor = colorVal, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };

			Label k3 = new Label { Text = "Init. time:", Font = fontKey, ForeColor = colorKey, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
			lblEstInitTime = new Label { Text = "1.3 ms", Font = fontVal, ForeColor = colorVal, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };

			Label k4 = new Label { Text = "CPU usage:", Font = fontKey, ForeColor = colorKey, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
			lblEstCpuUsage = new Label { Text = "1.2 % (one core)", Font = fontVal, ForeColor = colorVal, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };

			tlp.Controls.Add(k1, 0, 0);
			tlp.Controls.Add(lblEstPeakGain, 1, 0);
			tlp.Controls.Add(k2, 0, 1);
			tlp.Controls.Add(lblEstLatency, 1, 1);
			tlp.Controls.Add(k3, 0, 2);
			tlp.Controls.Add(lblEstInitTime, 1, 2);
			tlp.Controls.Add(k4, 0, 3);
			tlp.Controls.Add(lblEstCpuUsage, 1, 3);

			pnlBox.Controls.Add(tlp);
			body.Controls.Add(pnlBox);
			card.Controls.Add(body);
			body.BringToFront();

			UpdateEstimatedProperties();

			return card;
		}

		private Button CreateButton(string text, Color bg, int width, int height)
		{
			Button button = new Button();
			button.Text = text;
			button.BackColor = bg;
			button.ForeColor = Color.White;
			button.FlatStyle = FlatStyle.Flat;
			button.Width = width;
			button.Height = height;
			button.Cursor = Cursors.Hand;
			button.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
			Button button2 = button;
			button2.FlatAppearance.BorderSize = 0;
			return button2;
		}

		public void SyncCompControls()
		{
			if (sliderThresh != null)
			{
				sliderThresh.Value = Math.Max(-60.0, Math.Min(0.0, sliderThresh.Value));
				if (txtThreshVal != null)
				{
					txtThreshVal.Text = sliderThresh.Value.ToString("0.0");
				}
			}
		}

		public void LaunchOfficialDeviceSelector()
		{
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string[] array = new string[5]
			{
				Path.Combine(baseDirectory, "engine", "DeviceSelector.exe"),
				Path.Combine(baseDirectory, "DeviceSelector.exe"),
				"C:\\Program Files\\DolphinXVDO\\engine\\DeviceSelector.exe",
				"C:\\Program Files\\EqualizerAPO\\DeviceSelector.exe",
				"C:\\Program Files\\EqualizerAPO\\Configurator.exe"
			};
			string text = null;
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				if (File.Exists(text2))
				{
					text = text2;
					break;
				}
			}
			if (text != null)
			{
				try
				{
					Process p = new Process();
					p.StartInfo.FileName = text;
					string directoryName = Path.GetDirectoryName(text);
					p.StartInfo.WorkingDirectory = directoryName;
					string text3 = Path.Combine(directoryName, "DeviceSelector.qss");
					if (!File.Exists(text3))
					{
						text3 = Path.Combine(baseDirectory, "engine", "DeviceSelector.qss");
					}
					if (File.Exists(text3))
					{
						p.StartInfo.Arguments = "-stylesheet \"" + text3 + "\"";
					}
					p.StartInfo.UseShellExecute = true;
					p.Start();
					ThreadPool.QueueUserWorkItem(delegate
					{
						try
						{
							for (int j = 0; j < 60; j++)
							{
								Thread.Sleep(50);
								if (p.HasExited)
								{
									break;
								}
								p.Refresh();
								IntPtr intPtr = p.MainWindowHandle;
								if (intPtr == IntPtr.Zero)
								{
									intPtr = FindWindow("DeviceSelectorClass", null);
								}
								if (intPtr != IntPtr.Zero)
								{
									ApplyDiscordDarkStyle(intPtr);
									break;
								}
							}
						}
						catch
						{
						}
					});
				}
				catch (Exception ex)
				{
					MessageBox.Show("Failed to launch Equalizer APO Device Selector:\n" + ex.Message, "Device Selector", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
			}
			else
			{
				MessageBox.Show("DeviceSelector.exe not found.\nPlease install Equalizer APO or verify the engine folder.", "Device Selector", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}

		private void DrawFrequencyResponseGraph(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			int width = pnlGraph.Width;
			int height = pnlGraph.Height;
			if (width <= 4 || height <= 4)
			{
				return;
			}
			RectangleF clipRect = new RectangleF(0f, 0f, (float)width, (float)height);
			using (GraphicsPath clipPath = UiTheme.CreateRoundedPath(clipRect, 6f))
			{
				graphics.SetClip(clipPath);
				using (SolidBrush bgBr = new SolidBrush(Color.FromArgb(6, 6, 6)))
				{
					graphics.FillRectangle(bgBr, clipRect);
				}
				// Subtle radial accent glow from the Dark Tech spec
				using (GraphicsPath glowEllipse = new GraphicsPath())
				{
					glowEllipse.AddEllipse((float)width * 0.12f, -(float)height * 0.45f, (float)width * 0.76f, (float)height * 1.45f);
					using (PathGradientBrush pgb = new PathGradientBrush(glowEllipse))
					{
						pgb.CenterColor = Color.FromArgb(24, 82, 168, 255);
						pgb.SurroundColors = new Color[] { Color.FromArgb(0, 0, 0, 0) };
						graphics.FillPath(pgb, glowEllipse);
					}
				}
				using (Pen pen = new Pen(Color.FromArgb(24, 24, 24), 1f))
				using (SolidBrush lblBrush = new SolidBrush(Color.FromArgb(115, 115, 115)))
				{
					for (int i = -30; i <= 30; i += 10)
					{
						float y = (float)height / 2f - (float)i / 35f * ((float)height / 2f);
						graphics.DrawLine(pen, 0f, y, width, y);
						float lblY = Math.Max(3f, Math.Min((float)height - 22f, y - 10f));
						graphics.DrawString(string.Format("{0:+0;-0;0}dB", i), fontGraph, lblBrush, 7f, lblY);
					}
					for (int j = 0; j < graphFreqMarks.Length; j++)
					{
						double num2 = graphFreqMarks[j];
						float num3 = (float)((Math.Log10(num2) - Math.Log10(20.0)) / (Math.Log10(20000.0) - Math.Log10(20.0)) * (double)width);
						graphics.DrawLine(pen, num3, 0f, num3, height);
						string str = ((num2 >= 1000.0) ? (num2 / 1000.0 + "k") : num2.ToString());
						string text = str + "Hz";
						float width2 = graphics.MeasureString(text, fontGraph).Width;
						float x = ((num3 + width2 + 6f > (float)width) ? ((float)width - width2 - 5f) : (num3 + 3f));
						graphics.DrawString(text, fontGraph, lblBrush, x, height - 15);
					}
				}
				graphics.DrawLine(penGridDash, 0f, (float)height / 2f, width, (float)height / 2f);
				if (masterBypass)
				{
					graphics.DrawLine(penBypassRed, 0f, (float)height / 2f, width, (float)height / 2f);
					return;
				}
				int num4 = Math.Min(180, Math.Max(90, width / 2));
				PointF[] pts = new PointF[num4 + 1];
				for (int k = 0; k <= num4; k++)
				{
					double num5 = (double)k / (double)num4;
					double freq = Math.Pow(10.0, Math.Log10(20.0) + num5 * (Math.Log10(20000.0) - Math.Log10(20.0)));
					double num6 = preampDb;
					if (btnEqPower == null || btnEqPower.Checked)
					{
						foreach (BiquadFilter filter in filters)
						{
							num6 += filter.GetMagnitudeDbAt(freq);
						}
					}
					float num3 = (float)(num5 * (double)width);
					float num = (float)((double)((float)height / 2f) - num6 / 35.0 * (double)((float)height / 2f));
					num = Math.Max(3f, Math.Min(height - 3, num));
					pts[k] = new PointF(num3, num);
				}
				if (pts.Length <= 1)
				{
					return;
				}
				using (GraphicsPath graphicsPath = new GraphicsPath())
				{
					graphicsPath.AddLines(pts);
					graphicsPath.AddLine(pts[pts.Length - 1].X, height, pts[0].X, height);
					graphicsPath.CloseFigure();
					using (LinearGradientBrush brush = new LinearGradientBrush(new Point(0, 0), new Point(0, height), Color.FromArgb(42, 82, 168, 255), Color.FromArgb(0, 0, 0, 0)))
					{
						graphics.FillPath(brush, graphicsPath);
					}
				}
				graphics.DrawLines(penCurveCyan, pts);
			}
		}

		private void InitializeTrayIcon()
		{
			try
			{
				trayMenu = new ContextMenuStrip();
				trayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
				trayMenu.ShowImageMargin = true;
				trayMenu.ShowCheckMargin = false;
				ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("  Open DolphinXvdo");
				toolStripMenuItem.Font = UiTheme.DisplayFont(9f, FontStyle.Bold);
				toolStripMenuItem.ForeColor = UiTheme.TextPrimary;
				toolStripMenuItem.Click += delegate
				{
					ShowMainWindow();
				};
				ToolStripMenuItem itemBypass = new ToolStripMenuItem("  Bypass All Processing");
				itemBypass.CheckOnClick = true;
				itemBypass.Checked = masterBypass;
				itemBypass.ForeColor = UiTheme.TextBody;
				itemBypass.Click += delegate
				{
					if (chkMasterBypass != null)
					{
						chkMasterBypass.Checked = itemBypass.Checked;
					}
				};
				ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem("  Audio Devices Setup...");
				toolStripMenuItem2.ForeColor = UiTheme.TextBody;
				toolStripMenuItem2.Click += delegate
				{
					LaunchOfficialDeviceSelector();
				};
				ToolStripMenuItem toolStripMenuItem3 = new ToolStripMenuItem("  Settings...");
				toolStripMenuItem3.ForeColor = UiTheme.TextBody;
				toolStripMenuItem3.Click += delegate
				{
					ShowMainWindow();
					OpenSettingsDialog();
				};
				ToolStripMenuItem toolStripMenuItem4 = new ToolStripMenuItem("  Exit");
				toolStripMenuItem4.ForeColor = UiTheme.TextMuted;
				toolStripMenuItem4.Click += delegate
				{
					allowClose = true;
					Close();
					Application.Exit();
				};
				trayMenu.Items.Add(toolStripMenuItem);
				trayMenu.Items.Add(new ToolStripSeparator());
				trayMenu.Items.Add(itemBypass);
				trayMenu.Items.Add(toolStripMenuItem2);
				trayMenu.Items.Add(toolStripMenuItem3);
				trayMenu.Items.Add(new ToolStripSeparator());
				trayMenu.Items.Add(toolStripMenuItem4);
				trayMenu.Opening += delegate
				{
					itemBypass.Checked = chkMasterBypass != null && chkMasterBypass.Checked;
				};
				trayIcon = new NotifyIcon();
				trayIcon.Text = "DolphinXvdo v2.3";
				if (base.Icon != null)
				{
					trayIcon.Icon = base.Icon;
				}
				else
				{
					try
					{
						trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
					}
					catch
					{
						trayIcon.Icon = System.Drawing.SystemIcons.Application;
					}
				}
				trayIcon.ContextMenuStrip = trayMenu;
				trayIcon.Visible = true;
				if (startMinimizedOnLaunch && !shownTrayBalloon)
				{
					shownTrayBalloon = true;
					try
					{
						trayIcon.ShowBalloonTip(3500, "DolphinXvdo", "DolphinXvdo is running in the System Tray. Click anytime to open.", ToolTipIcon.Info);
					}
					catch
					{
					}
				}
				trayIcon.DoubleClick += delegate
				{
					ShowMainWindow();
				};
				trayIcon.MouseClick += delegate(object s, MouseEventArgs e)
				{
					if (e.Button == MouseButtons.Left)
					{
						ShowMainWindow();
					}
				};
			}
			catch
			{
			}
		}

		public void ShowMainWindow()
		{
			hasShownOnce = true;
			startMinimizedOnLaunch = false;

			base.WindowState = FormWindowState.Normal;
			base.Visible = true;
			Show();
			try
			{
				ShowWindow(base.Handle, 9); // SW_RESTORE: Forces window out of minimized state
			}
			catch
			{
			}
			BringToFront();
			Activate();
			try
			{
				SetForegroundWindow(base.Handle);
			}
			catch
			{
			}

			if (vuTimer != null && !vuTimer.Enabled)
			{
				vuTimer.Start();
			}
			if (usageTimer != null && !usageTimer.Enabled)
			{
				usageTimer.Start();
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (!allowClose && e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				Hide();
				if (vuTimer != null)
				{
					vuTimer.Stop();
				}
				if (usageTimer != null)
				{
					usageTimer.Stop();
				}
				SyncToAPO();
				if (!shownTrayBalloon && trayIcon != null)
				{
					shownTrayBalloon = true;
					try
					{
						trayIcon.ShowBalloonTip(3000, "DolphinXvdo", "DolphinXvdo is running in the System Tray. Click the icon anytime to open the window.", ToolTipIcon.Info);
					}
					catch
					{
					}
				}
				return;
			}
			SyncToAPO();
			if (trayIcon != null)
			{
				try
				{
					trayIcon.Visible = false;
					trayIcon.Dispose();
					trayIcon = null;
				}
				catch
				{
				}
			}
			base.OnFormClosing(e);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (vuTimer != null)
				{
					try
					{
						vuTimer.Stop();
						vuTimer.Dispose();
					}
					catch
					{
					}
					vuTimer = null;
				}
				if (usageTimer != null)
				{
					try
					{
						usageTimer.Stop();
						usageTimer.Dispose();
					}
					catch
					{
					}
					usageTimer = null;
				}
				if (deviceCheckTimer != null)
				{
					try
					{
						deviceCheckTimer.Stop();
						deviceCheckTimer.Dispose();
					}
					catch
					{
					}
					deviceCheckTimer = null;
				}
				if (pMeterChannelsMem != IntPtr.Zero)
				{
					try
					{
						Marshal.FreeHGlobal(pMeterChannelsMem);
					}
					catch
					{
					}
					pMeterChannelsMem = IntPtr.Zero;
				}
				if (trayIcon != null)
				{
					try
					{
						trayIcon.Visible = false;
						trayIcon.Dispose();
					}
					catch
					{
					}
					trayIcon = null;
				}
				if (syncTimer != null)
				{
					try
					{
						syncTimer.Dispose();
					}
					catch
					{
					}
					syncTimer = null;
				}
				if (stateSaveTimer != null)
				{
					try
					{
						stateSaveTimer.Dispose();
					}
					catch
					{
					}
					stateSaveTimer = null;
				}
				if (fontGraph != null)
				{
					try
					{
						fontGraph.Dispose();
					}
					catch
					{
					}
					fontGraph = null;
				}
			}
			base.Dispose(disposing);
		}

		public void TriggerSync(bool immediate = false)
		{
			if (isInitializing) return;
			isPeakGainDirty = true;
			UpdateEstimatedProperties();

			if (immediate)
			{
				syncPending = false;
				syncTimer.Stop();
				lastSyncTick = Environment.TickCount;
				SyncAudioConfigOnly();
				stateSaveTimer.Stop();
				stateSaveTimer.Start();
				return;
			}

			// Smooth real-time audio sync with 25ms throttle (~40 updates/sec):
			// When moving sliders, APO updates audio in real time as you drag smoothly!
			int now = Environment.TickCount;
			int elapsed = now - lastSyncTick;
			if (elapsed >= SYNC_THROTTLE_MS || elapsed < 0)
			{
				syncPending = false;
				syncTimer.Stop();
				lastSyncTick = now;
				SyncAudioConfigOnly();
			}
			else
			{
				syncPending = true;
				if (!syncTimer.Enabled)
				{
					syncTimer.Start();
				}
			}

			// Debounce state saving (INI file writing) to disk:
			// Prevents disk thrashing during rapid mouse drag, but commits within 350ms of stopping
			stateSaveTimer.Stop();
			stateSaveTimer.Start();
		}

		public void SaveDedicatedState()
		{
			try
			{
				StringWriter stringWriter = new StringWriter();
				stringWriter.WriteLine("[State]");
				string currentPresetName = ((cbPresets != null && cbPresets.SelectedItem != null) ? cbPresets.SelectedItem.ToString() : "Default");
				stringWriter.WriteLine("CurrentPreset=" + currentPresetName);
				stringWriter.WriteLine("MasterBypass=" + (masterBypass ? "True" : "False"));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "Preamp={0:0.0}", preampDb));
				stringWriter.WriteLine("EqPower=" + ((btnEqPower != null && btnEqPower.Checked) ? "True" : "False"));
				stringWriter.WriteLine("CompPower=" + (chkCompPower.Checked ? "True" : "False"));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompThresh={0:0.0}", sliderThresh.Value));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRatio={0:0.0}", CompRatio));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompAttack={0:0.0}", CompAttack));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRelease={0:0.0}", CompRelease));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompPreComp={0:0.0}", CompPreComp));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompKnee={0:0.0}", CompKnee));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompWet={0:0.0}", CompWet));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompDry={0:0.0}", CompDry));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRms={0:0.0}", CompRms));
				stringWriter.WriteLine("CompAutoMakeup=" + (CompAutoMakeup ? "True" : "False"));
				stringWriter.WriteLine("CompAutoRelease=" + (CompAutoRelease ? "True" : "False"));
				stringWriter.WriteLine("CompClassicAttack=" + (CompClassicAttack ? "True" : "False"));
				stringWriter.WriteLine("[Filters]");
				for (int i = 0; i < filters.Count; i++)
				{
					BiquadFilter biquadFilter = filters[i];
					stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "Band{0}={1},{2:0},{3:0.0},{4:0.00}", i + 1, biquadFilter.Type, biquadFilter.Frequency, biquadFilter.Gain, biquadFilter.Q));
				}
				if (CustomVstPlugins != null && CustomVstPlugins.Count > 0)
				{
					stringWriter.WriteLine("[CustomVST]");
					for (int k = 0; k < CustomVstPlugins.Count; k++)
					{
						stringWriter.WriteLine(string.Format("Plugin{0}={1}|{2}|{3}|{4}", k + 1, CustomVstPlugins[k].Name, CustomVstPlugins[k].FilePath, CustomVstPlugins[k].Enabled ? "1" : "0", CustomVstPlugins[k].ChunkData ?? ""));
					}
				}
				string contents = stringWriter.ToString();
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DolphinXVDO");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				File.WriteAllText(Path.Combine(text, "dolphinxvdo_state.ini"), contents);

				string baseConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
				if (!Directory.Exists(baseConfig))
				{
					Directory.CreateDirectory(baseConfig);
				}
				File.WriteAllText(Path.Combine(baseConfig, "dolphinxvdo_state.ini"), contents);
			}
			catch
			{
			}
		}

		private string GetPresetsDirectory()
		{
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string text = Path.Combine(baseDirectory, "config", "presets");
			if (!Directory.Exists(text))
			{
				try
				{
					Directory.CreateDirectory(text);
				}
				catch
				{
				}
			}
			return text;
		}

		public string GetAppDataDirectory()
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DolphinXVDO");
			if (!Directory.Exists(text))
			{
				try
				{
					Directory.CreateDirectory(text);
				}
				catch { }
			}
			return text;
		}

		private string GetAppDataPresetsDirectory()
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DolphinXVDO", "presets");
			if (!Directory.Exists(text))
			{
				try
				{
					Directory.CreateDirectory(text);
				}
				catch
				{
				}
			}
			return text;
		}

		private string GetDeletedPresetsFilePath()
		{
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DolphinXVDO", "deleted_presets.txt");
		}

		private HashSet<string> GetDeletedPresets()
		{
			HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				string path = GetDeletedPresetsFilePath();
				if (File.Exists(path))
				{
					string[] array = File.ReadAllLines(path);
					foreach (string text in array)
					{
						string text2 = text.Trim();
						if (!string.IsNullOrEmpty(text2))
						{
							set.Add(text2);
						}
					}
				}
			}
			catch
			{
			}
			return set;
		}

		private void AddDeletedPreset(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			try
			{
				HashSet<string> deletedPresets = GetDeletedPresets();
				if (deletedPresets.Add(name))
				{
					string deletedPresetsFilePath = GetDeletedPresetsFilePath();
					string directoryName = Path.GetDirectoryName(deletedPresetsFilePath);
					if (!Directory.Exists(directoryName))
					{
						Directory.CreateDirectory(directoryName);
					}
					File.WriteAllLines(deletedPresetsFilePath, new List<string>(deletedPresets).ToArray());
				}
			}
			catch
			{
			}
		}

		private void RemoveDeletedPreset(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			try
			{
				HashSet<string> deletedPresets = GetDeletedPresets();
				if (deletedPresets.Remove(name))
				{
					string deletedPresetsFilePath = GetDeletedPresetsFilePath();
					File.WriteAllLines(deletedPresetsFilePath, new List<string>(deletedPresets).ToArray());
				}
			}
			catch
			{
			}
		}

		private void EnsureDefaultPreset(string presetName, string contents)
		{
			try
			{
				HashSet<string> deletedPresets = GetDeletedPresets();
				if (deletedPresets.Contains(presetName))
				{
					return;
				}
				string presetsDirectory = GetPresetsDirectory();
				string appDataPresetsDirectory = GetAppDataPresetsDirectory();
				string text = Path.Combine(presetsDirectory, presetName + ".ini");
				string text2 = Path.Combine(appDataPresetsDirectory, presetName + ".ini");
				if (!File.Exists(text) && !File.Exists(text2))
				{
					try
					{
						File.WriteAllText(text, contents);
					}
					catch
					{
					}
					try
					{
						File.WriteAllText(text2, contents);
					}
					catch
					{
					}
				}
				else if (File.Exists(text) && !File.Exists(text2))
				{
					try
					{
						File.Copy(text, text2, true);
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		private void EnsureDefaultPresetExists()
		{
			EnsureDefaultPreset("Default", "[Preset]\r\nName=Default\r\nPreamp=0.0\r\nEqPower=False\r\nCompPower=True\r\nCompThresh=-22.0\r\nCompRatio=15.0\r\nCompAttack=5.0\r\nCompRelease=160.0\r\nCompPreComp=2.0\r\nCompKnee=0.0\r\nCompWet=0.0\r\nCompDry=-32.0\r\nCompRms=0.0\r\nCompAutoMakeup=False\r\nCompAutoRelease=False\r\nCompClassicAttack=False\r\n[Filters]\r\nBand1=31,0.0,1.41\r\nBand2=63,0.0,1.41\r\nBand3=125,0.0,1.41\r\nBand4=250,0.0,1.41\r\nBand5=500,0.0,1.41\r\nBand6=1000,0.0,1.41\r\nBand7=2000,0.0,1.41\r\nBand8=4000,0.0,1.41\r\nBand9=8000,0.0,1.41\r\nBand10=16000,0.0,1.41\r\n");

			EnsureDefaultPreset("FPS Footstep Clarity", "[Preset]\r\nName=FPS Footstep Clarity\r\nPreamp=-4.0\r\nEqPower=True\r\nCompPower=True\r\nCompThresh=-32.0\r\nCompRatio=3.5\r\nCompAttack=15.0\r\nCompRelease=80.0\r\nCompPreComp=0.0\r\nCompKnee=6.0\r\nCompWet=0.0\r\nCompDry=-60.0\r\nCompRms=10.0\r\nCompAutoMakeup=False\r\nCompAutoRelease=False\r\nCompClassicAttack=False\r\n[Filters]\r\nBand1=PK,31,-6.0,1.41\r\nBand2=PK,63,-3.0,1.41\r\nBand3=PK,125,0.0,1.41\r\nBand4=PK,250,-2.0,1.41\r\nBand5=PK,500,0.0,1.41\r\nBand6=PK,1000,2.0,1.41\r\nBand7=PK,2000,6.0,1.41\r\nBand8=PK,4000,8.0,1.41\r\nBand9=PK,8000,4.0,1.41\r\nBand10=PK,16000,0.0,1.41\r\n");

			EnsureDefaultPreset("Deep Bass & Warmth", "[Preset]\r\nName=Deep Bass & Warmth\r\nPreamp=-3.0\r\nEqPower=True\r\nCompPower=False\r\nCompThresh=-30.0\r\nCompRatio=2.0\r\nCompAttack=100.0\r\nCompRelease=100.0\r\nCompPreComp=0.0\r\nCompKnee=5.0\r\nCompWet=0.0\r\nCompDry=-60.0\r\nCompRms=15.0\r\nCompAutoMakeup=False\r\nCompAutoRelease=False\r\nCompClassicAttack=False\r\n[Filters]\r\nBand1=PK,31,5.0,1.41\r\nBand2=PK,63,6.5,1.41\r\nBand3=PK,125,4.0,1.41\r\nBand4=PK,250,1.0,1.41\r\nBand5=PK,500,0.0,1.41\r\nBand6=PK,1000,0.0,1.41\r\nBand7=PK,2000,1.5,1.41\r\nBand8=PK,4000,2.0,1.41\r\nBand9=PK,8000,4.0,1.41\r\nBand10=PK,16000,5.0,1.41\r\n");

			EnsureDefaultPreset("Voice & Podcast", "[Preset]\r\nName=Voice & Podcast\r\nPreamp=0.0\r\nEqPower=True\r\nCompPower=True\r\nCompThresh=-26.0\r\nCompRatio=2.5\r\nCompAttack=40.0\r\nCompRelease=120.0\r\nCompPreComp=0.0\r\nCompKnee=4.0\r\nCompWet=0.0\r\nCompDry=-60.0\r\nCompRms=15.0\r\nCompAutoMakeup=False\r\nCompAutoRelease=False\r\nCompClassicAttack=False\r\n[Filters]\r\nBand1=PK,31,-12.0,1.41\r\nBand2=PK,63,-8.0,1.41\r\nBand3=PK,125,-3.0,1.41\r\nBand4=PK,250,-1.0,1.41\r\nBand5=PK,500,1.0,1.41\r\nBand6=PK,1000,3.5,1.41\r\nBand7=PK,2000,5.0,1.41\r\nBand8=PK,4000,4.0,1.41\r\nBand9=PK,8000,1.5,1.41\r\nBand10=PK,16000,-1.0,1.41\r\n");

			EnsureDefaultPreset("Night Mode", "[Preset]\r\nName=Night Mode\r\nPreamp=-2.0\r\nEqPower=True\r\nCompPower=True\r\nCompThresh=-36.0\r\nCompRatio=4.5\r\nCompAttack=20.0\r\nCompRelease=150.0\r\nCompPreComp=0.0\r\nCompKnee=8.0\r\nCompWet=0.0\r\nCompDry=-60.0\r\nCompRms=20.0\r\nCompAutoMakeup=False\r\nCompAutoRelease=False\r\nCompClassicAttack=False\r\n[Filters]\r\nBand1=PK,31,-6.0,1.41\r\nBand2=PK,63,-4.0,1.41\r\nBand3=PK,125,-1.0,1.41\r\nBand4=PK,250,0.0,1.41\r\nBand5=PK,500,1.5,1.41\r\nBand6=PK,1000,3.0,1.41\r\nBand7=PK,2000,4.0,1.41\r\nBand8=PK,4000,3.0,1.41\r\nBand9=PK,8000,0.0,1.41\r\nBand10=PK,16000,-2.0,1.41\r\n");
		}

		private void RefreshPresetList(string selectName)
		{
			if (cbPresets == null)
			{
				return;
			}
			bool flag = isInitializing;
			isInitializing = true;
			try
			{
				EnsureDefaultPresetExists();
				cbPresets.Items.Clear();
				HashSet<string> deletedPresets = GetDeletedPresets();
				HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				string appDataPresetsDirectory = GetAppDataPresetsDirectory();
				if (Directory.Exists(appDataPresetsDirectory))
				{
					string[] files = Directory.GetFiles(appDataPresetsDirectory, "*.ini");
					foreach (string path in files)
					{
						string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
						if (!deletedPresets.Contains(fileNameWithoutExtension))
						{
							hashSet.Add(fileNameWithoutExtension);
						}
					}
				}

				string presetsDirectory = GetPresetsDirectory();
				if (Directory.Exists(presetsDirectory))
				{
					string[] files = Directory.GetFiles(presetsDirectory, "*.ini");
					foreach (string path in files)
					{
						string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
						if (!deletedPresets.Contains(fileNameWithoutExtension))
						{
							hashSet.Add(fileNameWithoutExtension);
						}
					}
				}

				List<string> list = new List<string>(hashSet);
				bool hasDefault = list.RemoveAll(delegate(string x)
				{
					return x.Equals("Default", StringComparison.OrdinalIgnoreCase);
				}) > 0;
				list.Sort(StringComparer.OrdinalIgnoreCase);
				if (hasDefault)
				{
					list.Insert(0, "Default");
				}
				foreach (string item in list)
				{
					cbPresets.Items.Add(item);
				}
				if (!string.IsNullOrEmpty(selectName) && cbPresets.Items.Contains(selectName))
				{
					cbPresets.SelectedItem = selectName;
				}
				else if (cbPresets.Items.Contains("Default"))
				{
					cbPresets.SelectedItem = "Default";
				}
				else if (cbPresets.Items.Count > 0)
				{
					cbPresets.SelectedIndex = 0;
				}
			}
			finally
			{
				isInitializing = flag;
			}
		}

		private void PromptSavePreset()
		{
			Form prompt = new Form();
			try
			{
				prompt.Width = 360;
				prompt.Height = 180;
				prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
				prompt.Text = "Save Preset";
				prompt.StartPosition = FormStartPosition.CenterParent;
				prompt.BackColor = BG_MAIN;
				prompt.ForeColor = UiTheme.TextBody;
				prompt.MaximizeBox = false;
				prompt.MinimizeBox = false;
				prompt.HandleCreated += delegate
				{
					ApplyDiscordDarkStyle(prompt.Handle);
				};
				Label value = new Label
				{
					Left = 20,
					Top = 16,
					Text = "Preset Name:",
					AutoSize = true,
					Font = UiTheme.DisplayFont(9.5f, FontStyle.Bold),
					ForeColor = UiTheme.TextPrimary
				};
				TextBox textBox2 = new TextBox
				{
					ForeColor = UiTheme.TextPrimary,
					Font = UiTheme.MonoFont(9.5f, FontStyle.Bold),
					TextAlign = HorizontalAlignment.Left
				};
				ModernNumberBox txtWrap = new ModernNumberBox(textBox2, 6)
				{
					Left = 20,
					Top = 42,
					Width = 304,
					Height = 30
				};
				if (cbPresets.SelectedItem != null)
				{
					textBox2.Text = cbPresets.SelectedItem.ToString();
				}
				ModernActionButton button2 = new ModernActionButton
				{
					Text = "Save",
					Left = 136,
					Width = 90,
					Top = 88,
					Height = 32,
					CornerRadius = 6,
					NormalBackColor = UiTheme.CtaBg,
					HoverBackColor = UiTheme.CtaHoverBg,
					PressedBackColor = UiTheme.CtaPressedBg,
					NormalBorderColor = UiTheme.CtaBg,
					HoverBorderColor = UiTheme.AccentBlue,
					TextColor = UiTheme.CtaText,
					HoverTextColor = UiTheme.CtaText,
					Font = UiTheme.DisplayFont(9f, FontStyle.Bold)
				};
				ModernActionButton button4 = new ModernActionButton
				{
					Text = "Cancel",
					Left = 234,
					Width = 90,
					Top = 88,
					Height = 32,
					CornerRadius = 6,
					NormalBackColor = UiTheme.BgWell,
					HoverBackColor = UiTheme.BgChip,
					PressedBackColor = UiTheme.BgPressed,
					NormalBorderColor = UiTheme.BorderRing,
					HoverBorderColor = UiTheme.AccentAlert,
					TextColor = UiTheme.TextBody,
					HoverTextColor = UiTheme.TextPrimary,
					Font = UiTheme.DisplayFont(9f, FontStyle.Regular)
				};
				button2.Click += delegate
				{
					prompt.DialogResult = DialogResult.OK;
					prompt.Close();
				};
				button4.Click += delegate
				{
					prompt.DialogResult = DialogResult.Cancel;
					prompt.Close();
				};
				textBox2.KeyDown += delegate(object s, KeyEventArgs e)
				{
					if (e.KeyCode == Keys.Return)
					{
						prompt.DialogResult = DialogResult.OK;
						prompt.Close();
						e.SuppressKeyPress = true;
					}
					else if (e.KeyCode == Keys.Escape)
					{
						prompt.DialogResult = DialogResult.Cancel;
						prompt.Close();
						e.SuppressKeyPress = true;
					}
				};
				prompt.Controls.Add(value);
				prompt.Controls.Add(txtWrap);
				prompt.Controls.Add(button2);
				prompt.Controls.Add(button4);
				if (prompt.ShowDialog(this) == DialogResult.OK)
				{
					string text = textBox2.Text.Trim();
					if (!string.IsNullOrEmpty(text))
					{
						SaveCurrentPreset(text);
					}
				}
			}
			finally
			{
				if (prompt != null)
				{
					((IDisposable)prompt).Dispose();
				}
			}
		}

		private void SaveCurrentPreset(string name)
		{
			try
			{
				char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
				char[] array = invalidFileNameChars;
				foreach (char oldChar in array)
				{
					name = name.Replace(oldChar, '_');
				}
				StringWriter stringWriter = new StringWriter();
				stringWriter.WriteLine("[Preset]");
				stringWriter.WriteLine("Name=" + name);
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "Preamp={0:0.0}", preampDb));
				stringWriter.WriteLine("EqPower=" + ((btnEqPower != null && btnEqPower.Checked) ? "True" : "False"));
				stringWriter.WriteLine("CompPower=" + (chkCompPower.Checked ? "True" : "False"));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompThresh={0:0.0}", sliderThresh.Value));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRatio={0:0.0}", CompRatio));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompAttack={0:0.0}", CompAttack));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRelease={0:0.0}", CompRelease));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompPreComp={0:0.0}", CompPreComp));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompKnee={0:0.0}", CompKnee));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompWet={0:0.0}", CompWet));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompDry={0:0.0}", CompDry));
				stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRms={0:0.0}", CompRms));
				stringWriter.WriteLine("CompAutoMakeup=" + (CompAutoMakeup ? "True" : "False"));
				stringWriter.WriteLine("CompAutoRelease=" + (CompAutoRelease ? "True" : "False"));
				stringWriter.WriteLine("CompClassicAttack=" + (CompClassicAttack ? "True" : "False"));
				stringWriter.WriteLine("[Filters]");
				for (int j = 0; j < filters.Count; j++)
				{
					BiquadFilter biquadFilter = filters[j];
					stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "Band{0}={1},{2:0},{3:0.0},{4:0.00}", j + 1, biquadFilter.Type, biquadFilter.Frequency, biquadFilter.Gain, biquadFilter.Q));
				}
				string contents = stringWriter.ToString();
				string path = Path.Combine(GetPresetsDirectory(), name + ".ini");
				string path2 = Path.Combine(GetAppDataPresetsDirectory(), name + ".ini");
				RemoveDeletedPreset(name);
				try
				{
					File.WriteAllText(path2, contents);
				}
				catch
				{
				}
				try
				{
					File.WriteAllText(path, contents);
				}
				catch
				{
				}
				RefreshPresetList(name);
				MessageBox.Show("Preset \"" + name + "\" saved successfully!", "DolphinXvdo", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to save preset: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		private void ApplyPreset(string name)
		{
			try
			{
				string text = Path.Combine(GetPresetsDirectory(), name + ".ini");
				string text2 = Path.Combine(GetAppDataPresetsDirectory(), name + ".ini");
				string text3 = (File.Exists(text2) ? text2 : (File.Exists(text) ? text : null));
				if (text3 == null)
				{
					return;
				}
				bool flag = isInitializing;
				isInitializing = true;
				Dictionary<int, Tuple<string, double, double, double>> dictionary = new Dictionary<int, Tuple<string, double, double, double>>();
				// Default compressor settings
				chkCompPower.Checked = true;
				if (btnCompPower != null) btnCompPower.Checked = true;
				sliderThresh.Value = -22.0;
				txtThreshVal.Text = "-22.0";
				CompRatio = 15.0;
				CompAttack = 5.0;
				CompRelease = 160.0;
				CompPreComp = 2.0;
				CompKnee = 0.0;
				CompWet = 0.0;
				CompDry = -32.0;
				CompRms = 0.0;
				CompAutoMakeup = false;
				CompAutoRelease = false;
				CompClassicAttack = false;

				string[] array = File.ReadAllLines(text3);
				string[] array2 = array;
				foreach (string text4 in array2)
				{
					string text5 = text4.Trim();
					if (string.IsNullOrEmpty(text5) || text5.StartsWith("[") || text5.StartsWith("#"))
					{
						continue;
					}
					int num = text5.IndexOf('=');
					if (num <= 0)
					{
						continue;
					}
					string text6 = text5.Substring(0, num).Trim();
					string text7 = text5.Substring(num + 1).Trim();
					bool result2;
					if (text6.Equals("Preamp", StringComparison.OrdinalIgnoreCase))
					{
						double result;
						if (double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
						{
							preampDb = result;
							sliderPreamp.Value = result;
							txtPreampVal.Text = string.Format("{0:+0.0;-0.0;0.0}", preampDb);
							txtPreampVal.ForeColor = ((preampDb > 0.0) ? UiTheme.AccentGreen : ((preampDb < 0.0) ? UiTheme.AccentBlue : UiTheme.TextPrimary));
						}
					}
					else if (text6.Equals("CompPower", StringComparison.OrdinalIgnoreCase))
					{
						if (bool.TryParse(text7, out result2))
						{
							chkCompPower.Checked = result2;
							if (btnCompPower != null)
							{
								btnCompPower.Checked = result2;
							}
						}
					}
					else if (text6.Equals("EqPower", StringComparison.OrdinalIgnoreCase))
					{
						if (bool.TryParse(text7, out result2) && btnEqPower != null)
						{
							btnEqPower.Checked = result2;
						}
					}
					else if (text6.Equals("CompThresh", StringComparison.OrdinalIgnoreCase))
					{
						double result3;
						if (double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out result3))
						{
							sliderThresh.Value = result3;
							txtThreshVal.Text = result3.ToString("0.0");
						}
					}
					else if (text6.Equals("CompRatio", StringComparison.OrdinalIgnoreCase))
					{
						double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out CompRatio);
					}
					else if (text6.Equals("CompAttack", StringComparison.OrdinalIgnoreCase))
					{
						double result4;
						if (double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out result4))
						{
							CompAttack = result4;
						}
					}
					else if (text6.Equals("CompRelease", StringComparison.OrdinalIgnoreCase))
					{
						double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out CompRelease);
					}
					else if (text6.Equals("CompPreComp", StringComparison.OrdinalIgnoreCase))
					{
						double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out CompPreComp);
					}
					else if (text6.Equals("CompKnee", StringComparison.OrdinalIgnoreCase))
					{
						double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out CompKnee);
					}
					else if (text6.Equals("CompWet", StringComparison.OrdinalIgnoreCase))
					{
						double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out CompWet);
					}
					else if (text6.Equals("CompDry", StringComparison.OrdinalIgnoreCase))
					{
						double result5;
						if (double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out result5))
						{
							if (Math.Abs(result5) < 0.01)
							{
								result5 = -60.0;
							}
							CompDry = result5;
						}
					}
					else if (text6.Equals("CompRms", StringComparison.OrdinalIgnoreCase))
					{
						double.TryParse(text7, NumberStyles.Float, CultureInfo.InvariantCulture, out CompRms);
					}
					else if (text6.Equals("CompAutoMakeup", StringComparison.OrdinalIgnoreCase))
					{
						if (bool.TryParse(text7, out result2))
						{
							CompAutoMakeup = result2;
						}
					}
					else if (text6.Equals("CompAutoRelease", StringComparison.OrdinalIgnoreCase))
					{
						if (bool.TryParse(text7, out result2))
						{
							CompAutoRelease = result2;
						}
					}
					else if (text6.Equals("CompClassicAttack", StringComparison.OrdinalIgnoreCase))
					{
						if (bool.TryParse(text7, out result2))
						{
							CompClassicAttack = result2;
						}
					}
					else
					{
						int result6;
						if (!text6.StartsWith("Band", StringComparison.OrdinalIgnoreCase) || !int.TryParse(text6.Substring(4), out result6))
						{
							continue;
						}
						string[] array3 = text7.Split(',');
						if (array3.Length >= 3)
						{
							string item = "PK";
							int num2 = 0;
							if (array3.Length >= 4 && !char.IsDigit(array3[0].Trim()[0]) && array3[0].Trim()[0] != '-')
							{
								item = BiquadFilter.NormalizeType(array3[0].Trim());
								num2 = 1;
							}
							double result7;
							double.TryParse(array3[num2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result7);
							double result8;
							double.TryParse(array3[num2 + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result8);
							double result9;
							double.TryParse(array3[num2 + 2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result9);
							dictionary[result6] = Tuple.Create(item, result7, result8, result9);
						}
					}
				}
				if (dictionary.Count > 0)
				{
					List<int> list = new List<int>(dictionary.Keys);
					list.Sort();
					filters.Clear();
					foreach (int item2 in list)
					{
						if (filters.Count >= 15)
						{
							break;
						}
						Tuple<string, double, double, double> tuple = dictionary[item2];
						string item = tuple.Item1;
						double result7 = ((tuple.Item2 >= 20.0 && tuple.Item2 <= 22000.0) ? tuple.Item2 : 1000.0);
						double result8 = Math.Max(-30.0, Math.Min(30.0, tuple.Item3));
						double result9 = ((tuple.Item4 > 0.05 && tuple.Item4 <= 20.0) ? tuple.Item4 : 1.41);
						filters.Add(new BiquadFilter(item, result7, result8, result9));
					}
					RebuildBandUI();
				}
				SyncCompControls();
				pnlGraph.Invalidate();
				isInitializing = flag;
				isPeakGainDirty = true;
				SyncToAPO();
			}
			catch
			{
			}
		}

		private void DeleteCurrentPreset()
		{
			if (cbPresets.SelectedItem == null)
			{
				MessageBox.Show("Please select a preset to delete first.", "Delete Preset", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			string text = cbPresets.SelectedItem.ToString();
			if (string.Equals(text, "Default", StringComparison.OrdinalIgnoreCase))
			{
				MessageBox.Show("The 'Default' preset is protected and cannot be deleted.", "Protected Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			if (MessageBox.Show("Are you sure you want to delete preset \"" + text + "\"?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			try
			{
				string path = Path.Combine(GetPresetsDirectory(), text + ".ini");
				string path2 = Path.Combine(GetAppDataPresetsDirectory(), text + ".ini");
				try
				{
					if (File.Exists(path2))
					{
						File.Delete(path2);
					}
				}
				catch
				{
				}
				try
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
				catch
				{
				}
				AddDeletedPreset(text);
				RefreshPresetList("Default");
				ApplyPreset("Default");
				MessageBox.Show("Preset \"" + text + "\" deleted successfully.", "Delete Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to delete preset: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		private void ExportCurrentPreset()
		{
			try
			{
				string presetName = (cbPresets != null && cbPresets.SelectedItem != null) ? cbPresets.SelectedItem.ToString() : null;
				if (string.IsNullOrEmpty(presetName))
				{
					MessageBox.Show("Please select a preset to export first.", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				string fileContent = null;
				string text1 = Path.Combine(GetPresetsDirectory(), presetName + ".ini");
				string text2 = Path.Combine(GetAppDataPresetsDirectory(), presetName + ".ini");
				if (File.Exists(text2))
				{
					fileContent = File.ReadAllText(text2);
				}
				else if (File.Exists(text1))
				{
					fileContent = File.ReadAllText(text1);
				}

				if (string.IsNullOrEmpty(fileContent))
				{
					StringWriter sw = new StringWriter();
					sw.WriteLine("[Preset]");
					sw.WriteLine("Name=" + presetName);
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "Preamp={0:0.0}", preampDb));
					sw.WriteLine("EqPower=" + ((btnEqPower != null && btnEqPower.Checked) ? "True" : "False"));
					sw.WriteLine("CompPower=" + (chkCompPower.Checked ? "True" : "False"));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompThresh={0:0.0}", sliderThresh.Value));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRatio={0:0.0}", CompRatio));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompAttack={0:0.0}", CompAttack));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRelease={0:0.0}", CompRelease));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompPreComp={0:0.0}", CompPreComp));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompKnee={0:0.0}", CompKnee));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompWet={0:0.0}", CompWet));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompDry={0:0.0}", CompDry));
					sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "CompRms={0:0.0}", CompRms));
					sw.WriteLine("CompAutoMakeup=" + (CompAutoMakeup ? "True" : "False"));
					sw.WriteLine("CompAutoRelease=" + (CompAutoRelease ? "True" : "False"));
					sw.WriteLine("CompClassicAttack=" + (CompClassicAttack ? "True" : "False"));
					sw.WriteLine("[Filters]");
					for (int j = 0; j < filters.Count; j++)
					{
						BiquadFilter biquadFilter = filters[j];
						sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "Band{0}={1},{2:0},{3:0.0},{4:0.00}", j + 1, biquadFilter.Type, biquadFilter.Frequency, biquadFilter.Gain, biquadFilter.Q));
					}
					fileContent = sw.ToString();
				}

				SaveFileDialog sfd = new SaveFileDialog();
				try
				{
					sfd.Title = "Export Preset";
					sfd.Filter = "Preset Configuration (*.ini)|*.ini|All Files (*.*)|*.*";
					sfd.FileName = presetName + ".ini";
					sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
					if (sfd.ShowDialog(this) == DialogResult.OK)
					{
						File.WriteAllText(sfd.FileName, fileContent);
						MessageBox.Show(string.Format("Preset \"{0}\" exported successfully!\n\nSaved to:\n{1}", presetName, sfd.FileName), "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
				}
				finally
				{
					if (sfd != null)
					{
						((IDisposable)sfd).Dispose();
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to export preset: " + ex.Message, "Export Preset Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		private void MainForm_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files != null && files.Length > 0)
				{
					string ext = Path.GetExtension(files[0]).ToLowerInvariant();
					if (ext == ".txt" || ext == ".ini" || ext == ".csv")
					{
						e.Effect = DragDropEffects.Copy;
						return;
					}
				}
			}
			e.Effect = DragDropEffects.None;
		}

		private void MainForm_DragDrop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files != null && files.Length > 0)
				{
					ProcessImportFile(files[0]);
				}
			}
		}

		private void ImportPreset()
		{
			try
			{
				OpenFileDialog ofd = new OpenFileDialog();
				try
				{
					ofd.Title = "Import Preset or Squiglink / AutoEQ file";
					ofd.Filter = "Preset / AutoEQ (*.ini;*.txt;*.csv)|*.ini;*.txt;*.csv|Equalizer APO Preset (*.ini)|*.ini|Squiglink / AutoEQ Text (*.txt)|*.txt|All Files (*.*)|*.*";
					ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
					if (ofd.ShowDialog(this) == DialogResult.OK && File.Exists(ofd.FileName))
					{
						ProcessImportFile(ofd.FileName);
					}
				}
				finally
				{
					if (ofd != null)
					{
						((IDisposable)ofd).Dispose();
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to open file: " + ex.Message, "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		public void ProcessImportFile(string filePath)
		{
			try
			{
				if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
				{
					return;
				}

				string content = File.ReadAllText(filePath);
				if (string.IsNullOrEmpty(content))
				{
					MessageBox.Show("The selected file is empty.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				// Check if it is a standard INI preset
				if (content.Contains("[Preset]") || content.Contains("[Filters]") || (content.Contains("Band") && content.Contains("=")))
				{
					ImportIniPresetContent(filePath, content);
					return;
				}

				// Check if it is a Squiglink / AutoEQ text file
				if (content.IndexOf("Filter", StringComparison.OrdinalIgnoreCase) >= 0 ||
					content.IndexOf("Preamp", StringComparison.OrdinalIgnoreCase) >= 0 ||
					content.IndexOf("Fc", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					ImportSquiglinkContent(filePath, content);
					return;
				}

				MessageBox.Show("The selected file does not appear to be a recognized preset or Squiglink/AutoEQ EQ file.", "Unrecognized Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to import file: " + ex.Message, "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		private void ImportSquiglinkContent(string filePath, string content)
		{
			double importedPreamp = 0.0;
			List<BiquadFilter> importedFilters = new List<BiquadFilter>();

			string[] lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string rawLine in lines)
			{
				string line = rawLine.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

				// Preamp: -1.3 dB
				Match mPre = Regex.Match(line, @"Preamp:\s*([+-]?[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
				if (mPre.Success)
				{
					double pVal;
					if (double.TryParse(mPre.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out pVal))
					{
						importedPreamp = Math.Max(-30.0, Math.Min(30.0, pVal));
					}
					continue;
				}

				// Filter 1: ON PK Fc 30 Hz Gain 1.0 dB Q 0.500
				if (line.IndexOf("Filter", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					if (Regex.IsMatch(line, @"\bOFF\b", RegexOptions.IgnoreCase))
					{
						continue; // Skip inactive band
					}

					string fType = "PK";
					Match mType = Regex.Match(line, @"\b(PK|LS|HS|HP|LP|LSC|HSC|HPQ|LOWSHELF|HIGHSHELF)\b", RegexOptions.IgnoreCase);
					if (mType.Success)
					{
						fType = BiquadFilter.NormalizeType(mType.Groups[1].Value);
					}

					Match mFc = Regex.Match(line, @"Fc\s+([0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
					Match mGain = Regex.Match(line, @"Gain\s+([+-]?[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
					Match mQ = Regex.Match(line, @"Q\s+([0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);

					if (mFc.Success && mGain.Success)
					{
						double fc, gain, q = 1.41;
						double.TryParse(mFc.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out fc);
						double.TryParse(mGain.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out gain);
						if (mQ.Success)
						{
							double.TryParse(mQ.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out q);
						}

						if (fc >= 20.0 && fc <= 22000.0)
						{
							gain = Math.Max(-30.0, Math.Min(30.0, gain));
							q = Math.Max(0.1, Math.Min(20.0, q));
							importedFilters.Add(new BiquadFilter(fType, fc, gain, q));
						}
					}
				}
			}

			if (importedFilters.Count == 0)
			{
				MessageBox.Show("No valid EQ filters could be found in the Squiglink/AutoEQ file.", "Import Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			// Cap at 15 filters (DolphinX 15-band limit)
			if (importedFilters.Count > 15)
			{
				importedFilters = importedFilters.Take(15).ToList();
			}

			string presetName = Path.GetFileNameWithoutExtension(filePath);
			char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
			foreach (char c in invalidFileNameChars)
			{
				presetName = presetName.Replace(c, '_');
			}
			presetName = presetName.Trim();
			if (string.IsNullOrEmpty(presetName))
			{
				presetName = "AutoEQ_Import";
			}

			StringWriter sw = new StringWriter();
			sw.WriteLine("[Preset]");
			sw.WriteLine("Name=" + presetName);
			sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "Preamp={0:0.0}", importedPreamp));
			sw.WriteLine("EqPower=True");
			sw.WriteLine("CompPower=True");
			sw.WriteLine("CompThresh=-22.0");
			sw.WriteLine("CompRatio=15.0");
			sw.WriteLine("CompAttack=5.0");
			sw.WriteLine("CompRelease=160.0");
			sw.WriteLine("CompPreComp=2.0");
			sw.WriteLine("CompKnee=0.0");
			sw.WriteLine("CompWet=0.0");
			sw.WriteLine("CompDry=-32.0");
			sw.WriteLine("CompRms=0.0");
			sw.WriteLine("CompAutoMakeup=False");
			sw.WriteLine("CompAutoRelease=False");
			sw.WriteLine("CompClassicAttack=False");
			sw.WriteLine("[Filters]");
			for (int j = 0; j < importedFilters.Count; j++)
			{
				BiquadFilter bf = importedFilters[j];
				sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "Band{0}={1},{2:0},{3:0.0},{4:0.00}", j + 1, bf.Type, bf.Frequency, bf.Gain, bf.Q));
			}

			string destIni = sw.ToString();
			string destPath1 = Path.Combine(GetPresetsDirectory(), presetName + ".ini");
			string destPath2 = Path.Combine(GetAppDataPresetsDirectory(), presetName + ".ini");

			RemoveDeletedPreset(presetName);
			try { File.WriteAllText(destPath2, destIni); } catch { }
			try { File.WriteAllText(destPath1, destIni); } catch { }

			RefreshPresetList(presetName);
			ApplyPreset(presetName);

			MessageBox.Show(string.Format("Smart Import successful!\r\n\r\nPreset: \"{0}\"\r\nImported: {1} filters\r\nPreamp: {2:+0.0;-0.0;0.0} dB", presetName, importedFilters.Count, importedPreamp), "Smart Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void ImportIniPresetContent(string filePath, string content)
		{
			string presetName = null;
			string[] lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string line in lines)
			{
				string trimmed = line.Trim();
				if (trimmed.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
				{
					presetName = trimmed.Substring(5).Trim();
					break;
				}
			}
			if (string.IsNullOrEmpty(presetName))
			{
				presetName = Path.GetFileNameWithoutExtension(filePath);
			}

			char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
			foreach (char c in invalidFileNameChars)
			{
				presetName = presetName.Replace(c, '_');
			}
			presetName = presetName.Trim();
			if (string.IsNullOrEmpty(presetName))
			{
				presetName = "Imported_Preset";
			}

			// If the INI doesn't contain compressor parameters, inject the reference defaults
			if (!content.Contains("CompRatio"))
			{
				StringBuilder sb = new StringBuilder();
				bool addedComp = false;
				foreach (string l in lines)
				{
					if (!addedComp && l.Trim().StartsWith("[Filters]", StringComparison.OrdinalIgnoreCase))
					{
						sb.AppendLine("CompPower=True");
						sb.AppendLine("CompThresh=-22.0");
						sb.AppendLine("CompRatio=15.0");
						sb.AppendLine("CompAttack=5.0");
						sb.AppendLine("CompRelease=160.0");
						sb.AppendLine("CompPreComp=2.0");
						sb.AppendLine("CompKnee=0.0");
						sb.AppendLine("CompWet=0.0");
						sb.AppendLine("CompDry=-32.0");
						sb.AppendLine("CompRms=0.0");
						sb.AppendLine("CompAutoMakeup=False");
						sb.AppendLine("CompAutoRelease=False");
						sb.AppendLine("CompClassicAttack=False");
						addedComp = true;
					}
					sb.AppendLine(l);
				}
				if (!addedComp)
				{
					sb.AppendLine("CompPower=True");
					sb.AppendLine("CompThresh=-22.0");
					sb.AppendLine("CompRatio=15.0");
					sb.AppendLine("CompAttack=5.0");
					sb.AppendLine("CompRelease=160.0");
					sb.AppendLine("CompPreComp=2.0");
					sb.AppendLine("CompKnee=0.0");
					sb.AppendLine("CompWet=0.0");
					sb.AppendLine("CompDry=-32.0");
					sb.AppendLine("CompRms=0.0");
					sb.AppendLine("CompAutoMakeup=False");
					sb.AppendLine("CompAutoRelease=False");
					sb.AppendLine("CompClassicAttack=False");
				}
				content = sb.ToString();
			}

			string destPath1 = Path.Combine(GetPresetsDirectory(), presetName + ".ini");
			string destPath2 = Path.Combine(GetAppDataPresetsDirectory(), presetName + ".ini");
			if (File.Exists(destPath1) || File.Exists(destPath2))
			{
				DialogResult dr = MessageBox.Show(string.Format("Preset \"{0}\" already exists.\r\nDo you want to overwrite it?", presetName), "Confirm Overwrite", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
				if (dr != DialogResult.Yes)
				{
					return;
				}
			}

			RemoveDeletedPreset(presetName);
			try { File.WriteAllText(destPath2, content); } catch { }
			try { File.WriteAllText(destPath1, content); } catch { }

			RefreshPresetList(presetName);
			ApplyPreset(presetName);
			MessageBox.Show(string.Format("Preset \"{0}\" imported and applied successfully!", presetName), "Preset Imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void LoadSettingsAfterUI()
		{
			try
			{
				string textState1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DolphinXVDO", "dolphinxvdo_state.ini");
				string textState2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "dolphinxvdo_state.ini");
				string path = (File.Exists(textState1) ? textState1 : (File.Exists(textState2) ? textState2 : null));
				if (path != null && File.Exists(path))
				{
					string savedPreset = "Default";
					Dictionary<int, Tuple<string, double, double, double>> dictionary = new Dictionary<int, Tuple<string, double, double, double>>();
					string[] array = File.ReadAllLines(path);
					string[] array2 = array;
					foreach (string text in array2)
					{
						string text2 = text.Trim();
						if (string.IsNullOrEmpty(text2) || text2.StartsWith("[") || text2.StartsWith("#"))
						{
							continue;
						}
						int num = text2.IndexOf('=');
						if (num <= 0)
						{
							continue;
						}
						string text3 = text2.Substring(0, num).Trim();
						string text4 = text2.Substring(num + 1).Trim();
						bool result;
						if (text3.Equals("CurrentPreset", StringComparison.OrdinalIgnoreCase))
						{
							if (!string.IsNullOrEmpty(text4))
							{
								savedPreset = text4;
							}
						}
						else if (text3.Equals("MasterBypass", StringComparison.OrdinalIgnoreCase))
						{
							if (bool.TryParse(text4, out result))
							{
								masterBypass = result;
								chkMasterBypass.Checked = result;
							}
						}
						else if (text3.Equals("Preamp", StringComparison.OrdinalIgnoreCase))
						{
							double result2;
							if (double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out result2))
							{
								preampDb = result2;
								sliderPreamp.Value = result2;
								txtPreampVal.Text = string.Format("{0:+0.0;-0.0;0.0}", preampDb);
								txtPreampVal.ForeColor = ((preampDb > 0.0) ? UiTheme.AccentGreen : ((preampDb < 0.0) ? UiTheme.AccentBlue : UiTheme.TextPrimary));
							}
						}
						else if (text3.Equals("CompPower", StringComparison.OrdinalIgnoreCase))
						{
							if (bool.TryParse(text4, out result))
							{
								chkCompPower.Checked = result;
								if (btnCompPower != null)
								{
									btnCompPower.Checked = result;
								}
							}
						}
						else if (text3.Equals("EqPower", StringComparison.OrdinalIgnoreCase))
						{
							if (bool.TryParse(text4, out result) && btnEqPower != null)
							{
								btnEqPower.Checked = result;
							}
						}
						else if (text3.Equals("CompThresh", StringComparison.OrdinalIgnoreCase))
						{
							double result3;
							if (double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out result3))
							{
								sliderThresh.Value = result3;
								txtThreshVal.Text = result3.ToString("0.0");
							}
						}
						else if (text3.Equals("CompRatio", StringComparison.OrdinalIgnoreCase))
						{
							double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out CompRatio);
						}
						else if (text3.Equals("CompAttack", StringComparison.OrdinalIgnoreCase))
						{
							double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out CompAttack);
						}
						else if (text3.Equals("CompRelease", StringComparison.OrdinalIgnoreCase))
						{
							double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out CompRelease);
						}
						else if (text3.Equals("CompPreComp", StringComparison.OrdinalIgnoreCase))
						{
							double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out CompPreComp);
						}
						else if (text3.Equals("CompKnee", StringComparison.OrdinalIgnoreCase))
						{
							double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out CompKnee);
						}
						else if (text3.Equals("CompWet", StringComparison.OrdinalIgnoreCase))
						{
							double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out CompWet);
						}
						else if (text3.Equals("CompDry", StringComparison.OrdinalIgnoreCase))
						{
							double result4;
							if (double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out result4))
							{
								if (Math.Abs(result4) < 0.01)
								{
									result4 = -60.0;
								}
								CompDry = result4;
							}
						}
						else if (text3.Equals("CompRms", StringComparison.OrdinalIgnoreCase))
						{
							double.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out CompRms);
						}
						else if (text3.Equals("CompAutoMakeup", StringComparison.OrdinalIgnoreCase))
						{
							if (bool.TryParse(text4, out result))
							{
								CompAutoMakeup = result;
							}
						}
						else if (text3.Equals("CompAutoRelease", StringComparison.OrdinalIgnoreCase))
						{
							if (bool.TryParse(text4, out result))
							{
								CompAutoRelease = result;
							}
						}
						else if (text3.Equals("CompClassicAttack", StringComparison.OrdinalIgnoreCase))
						{
							if (bool.TryParse(text4, out result))
							{
								CompClassicAttack = result;
							}
						}
						else if (text3.StartsWith("Plugin", StringComparison.OrdinalIgnoreCase))
						{
							string[] parts = text4.Split('|');
							if (parts.Length >= 2)
							{
								string vName = parts[0].Trim();
								string vPath = parts[1].Trim();
								bool vEnabled = (parts.Length < 3 || parts[2].Trim() == "1" || parts[2].Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
								string vChunk = (parts.Length >= 4 ? parts[3].Trim() : "");
								if (File.Exists(vPath))
								{
									CustomVstPlugins.Add(new CustomVstItem(vName, vPath, vEnabled, vChunk));
								}
							}
						}
						else
						{
							int result5;
							if (!text3.StartsWith("Band", StringComparison.OrdinalIgnoreCase) || !int.TryParse(text3.Substring(4), out result5))
							{
								continue;
							}
							string[] array3 = text4.Split(',');
							if (array3.Length >= 3)
							{
								string item = "PK";
								int num2 = 0;
								if (array3.Length >= 4 && !char.IsDigit(array3[0].Trim()[0]) && array3[0].Trim()[0] != '-')
								{
									item = array3[0].Trim();
									num2 = 1;
								}
								double result6;
								double result7;
								if (double.TryParse(array3[num2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result6) && double.TryParse(array3[num2 + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result7))
								{
									double result8 = 1.41;
									if (array3.Length > num2 + 2)
									{
										double.TryParse(array3[num2 + 2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result8);
									}
									dictionary[result5] = new Tuple<string, double, double, double>(item, result6, result7, result8);
								}
							}
						}
					}
					if (dictionary.Count > 0)
					{
						List<int> list = new List<int>(dictionary.Keys);
						list.Sort();
						filters.Clear();
						foreach (int item2 in list)
						{
							if (filters.Count >= 15)
							{
								break;
							}
							Tuple<string, double, double, double> tuple = dictionary[item2];
							string item = tuple.Item1;
							double result6 = ((tuple.Item2 >= 20.0 && tuple.Item2 <= 22000.0) ? tuple.Item2 : 1000.0);
							double result7 = Math.Max(-30.0, Math.Min(30.0, tuple.Item3));
							double result8 = ((tuple.Item4 > 0.05 && tuple.Item4 <= 20.0) ? tuple.Item4 : 1.41);
							filters.Add(new BiquadFilter(item, result6, result7, result8));
						}
						RebuildBandUI();
					}
					SyncCompControls();
					pnlGraph.Invalidate();
					isPeakGainDirty = true;
					RefreshPresetList(savedPreset);
					UpdateVstRackButtonText();
				}
				else
				{
					ApplyPreset("Default");
					RefreshPresetList("Default");
					UpdateVstRackButtonText();
				}
			}
			catch
			{
			}
		}

		public List<string> GetAvailablePresetNames()
		{
			List<string> list = new List<string>();
			if (cbPresets != null && cbPresets.Items.Count > 0)
			{
				foreach (object item in cbPresets.Items)
				{
					if (item != null)
					{
						string s = item.ToString().Trim();
						if (!string.IsNullOrEmpty(s) && !list.Contains(s))
						{
							list.Add(s);
						}
					}
				}
			}
			if (list.Count == 0)
			{
				string pDir = GetAppDataPresetsDirectory();
				if (Directory.Exists(pDir))
				{
					foreach (string file in Directory.GetFiles(pDir, "*.ini"))
					{
						string name = Path.GetFileNameWithoutExtension(file);
						if (!list.Contains(name)) list.Add(name);
					}
				}
			}
			return list;
		}

		public void LoadDevicePresetSettings()
		{
			try
			{
				string path1 = Path.Combine(GetAppDataDirectory(), "device_presets.ini");
				string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "device_presets.ini");
				string path = File.Exists(path1) ? path1 : (File.Exists(path2) ? path2 : null);

				if (path != null && File.Exists(path))
				{
					string[] lines = File.ReadAllLines(path);
					string section = "";
					foreach (string rawLine in lines)
					{
						string line = rawLine.Trim();
						if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
						if (line.StartsWith("[") && line.EndsWith("]"))
						{
							section = line.Substring(1, line.Length - 2).Trim();
							continue;
						}
						int eqIdx = line.IndexOf('=');
						if (eqIdx > 0)
						{
							string key = line.Substring(0, eqIdx).Trim();
							string val = line.Substring(eqIdx + 1).Trim();
							if (section.Equals("Settings", StringComparison.OrdinalIgnoreCase))
							{
								if (key.Equals("AutoSwitchEnabled", StringComparison.OrdinalIgnoreCase))
								{
									bool b;
									if (bool.TryParse(val, out b)) AutoSwitchPresetByDevice = b;
								}
							}
							else if (section.Equals("DeviceMap", StringComparison.OrdinalIgnoreCase) ||
							         section.Equals("DeviceNameMap", StringComparison.OrdinalIgnoreCase))
							{
								if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
								{
									DevicePresetMap[key] = val;
								}
							}
						}
					}
				}
			}
			catch { }
		}

		public void SaveDevicePresetSettings()
		{
			try
			{
				string dir = GetAppDataDirectory();
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				string path = Path.Combine(dir, "device_presets.ini");

				using (StringWriter sw = new StringWriter())
				{
					sw.WriteLine("[Settings]");
					sw.WriteLine("AutoSwitchEnabled=" + AutoSwitchPresetByDevice.ToString());
					sw.WriteLine("");
					sw.WriteLine("[DeviceMap]");
					foreach (KeyValuePair<string, string> kvp in DevicePresetMap)
					{
						if (kvp.Key.StartsWith("{"))
						{
							sw.WriteLine(kvp.Key + "=" + kvp.Value);
						}
					}
					sw.WriteLine("");
					sw.WriteLine("[DeviceNameMap]");
					foreach (KeyValuePair<string, string> kvp in DevicePresetMap)
					{
						if (!kvp.Key.StartsWith("{"))
						{
							sw.WriteLine(kvp.Key + "=" + kvp.Value);
						}
					}
					string content = sw.ToString();
					File.WriteAllText(path, content);
					try
					{
						string cfgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
						if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
						File.WriteAllText(Path.Combine(cfgDir, "device_presets.ini"), content);
					}
					catch { }
				}
			}
			catch { }
		}

		private void InitializeDeviceCheckTimer()
		{
			try
			{
				CheckDefaultAudioDeviceChange();

				deviceCheckTimer = new System.Windows.Forms.Timer
				{
					Interval = 1000
				};
				deviceCheckTimer.Tick += delegate
				{
					CheckDefaultAudioDeviceChange();
				};
				deviceCheckTimer.Start();
			}
			catch { }
		}

		public void CheckDefaultAudioDeviceChange()
		{
			if (!AutoSwitchPresetByDevice) return;
			try
			{
				IMMDeviceEnumerator iMMDeviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
				IMMDevice ppDevice;
				if (iMMDeviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out ppDevice) == 0 && ppDevice != null)
				{
					string id;
					if (ppDevice.GetId(out id) == 0 && !string.IsNullOrEmpty(id))
					{
						if (lastDetectedDefaultDeviceId == null)
						{
							lastDetectedDefaultDeviceId = id;
							return;
						}
						if (!string.Equals(id, lastDetectedDefaultDeviceId, StringComparison.OrdinalIgnoreCase))
						{
							lastDetectedDefaultDeviceId = id;
							ApplyPresetForDevice(id);
						}
					}
				}
			}
			catch { }
		}

		public void ApplyPresetForDevice(string fullDeviceId)
		{
			if (string.IsNullOrEmpty(fullDeviceId) || DevicePresetMap == null || DevicePresetMap.Count == 0) return;

			string matchedPreset = null;

			// 1. Check exact ID or GUID substring
			foreach (KeyValuePair<string, string> kvp in DevicePresetMap)
			{
				if (!string.IsNullOrEmpty(kvp.Key) && fullDeviceId.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					matchedPreset = kvp.Value;
					break;
				}
			}

			// 2. Fallback: match by friendly device name or adapter
			List<AudioDeviceInfo> devs = null;
			if (string.IsNullOrEmpty(matchedPreset))
			{
				devs = ModernAudioDevicesDialog.QueryAudioDevicesDirect();
				AudioDeviceInfo currentDev = devs.FirstOrDefault(d => fullDeviceId.IndexOf(d.Id, StringComparison.OrdinalIgnoreCase) >= 0);
				if (currentDev != null)
				{
					foreach (KeyValuePair<string, string> kvp in DevicePresetMap)
					{
						if (!string.IsNullOrEmpty(kvp.Key) && (string.Equals(currentDev.Name, kvp.Key, StringComparison.OrdinalIgnoreCase) ||
							string.Equals(currentDev.Adapter, kvp.Key, StringComparison.OrdinalIgnoreCase)))
						{
							matchedPreset = kvp.Value;
							break;
						}
					}
				}
			}

			if (!string.IsNullOrEmpty(matchedPreset) && !matchedPreset.Equals("(No Change)", StringComparison.OrdinalIgnoreCase))
			{
				if (cbPresets != null && cbPresets.Items.Contains(matchedPreset))
				{
					string currentSelected = (cbPresets.SelectedItem != null) ? cbPresets.SelectedItem.ToString() : "";
					if (!string.Equals(currentSelected, matchedPreset, StringComparison.OrdinalIgnoreCase))
					{
						cbPresets.SelectedItem = matchedPreset;
						InitWasapiMeter();

						if (trayIcon != null)
						{
							string devName = "Audio Output";
							if (devs == null) devs = ModernAudioDevicesDialog.QueryAudioDevicesDirect();
							AudioDeviceInfo cd = devs.FirstOrDefault(d => fullDeviceId.IndexOf(d.Id, StringComparison.OrdinalIgnoreCase) >= 0);
							if (cd != null && !string.IsNullOrEmpty(cd.Name)) devName = cd.Name;

							trayIcon.ShowBalloonTip(2000, "DolphinXvdo Auto-Switch", string.Format("Switched preset to \"{0}\" ({1})", matchedPreset, devName), ToolTipIcon.Info);
						}
					}
				}
			}
		}

		private void EnsureReaCompInstalled()
		{
			try
			{
				string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
				string[] array = new string[7]
				{
					Path.Combine(baseDirectory, "engine", "VSTPlugins", "reacomp-standalone.dll"),
					Path.Combine(baseDirectory, "plugins", "reacomp-standalone.dll"),
					Path.Combine(baseDirectory, "reacomp-standalone.dll"),
					"C:\\Program Files\\DolphinXVDO\\plugins\\reacomp-standalone.dll",
					"C:\\Program Files\\DolphinXVDO\\engine\\VSTPlugins\\reacomp-standalone.dll",
					"C:\\Program Files\\EqualizerAPO\\VSTPlugins\\reacomp-standalone.dll",
					"C:\\Program Files\\EqualizerAPO\\plugins\\reacomp-standalone.dll"
				};
				string text = null;
				string[] array2 = array;
				foreach (string text2 in array2)
				{
					if (File.Exists(text2))
					{
						text = text2;
						break;
					}
				}
				if (string.IsNullOrEmpty(text))
				{
					return;
				}
				string[] array3 = new string[4]
				{
					"C:\\Program Files\\DolphinXVDO\\engine\\VSTPlugins",
					"C:\\Program Files\\DolphinXVDO\\plugins",
					Path.Combine(baseDirectory, "engine", "VSTPlugins"),
					Path.Combine(baseDirectory, "plugins")
				};
				array2 = array3;
				foreach (string text3 in array2)
				{
					try
					{
						if (!Directory.Exists(text3))
						{
							Directory.CreateDirectory(text3);
						}
						string text4 = Path.Combine(text3, "reacomp-standalone.dll");
						if (!File.Exists(text4) || new FileInfo(text4).Length != new FileInfo(text).Length)
						{
							File.Copy(text, text4, true);
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		private string cachedReaCompDllPath = null;

		private string GetReaCompDllPath()
		{
			if (cachedReaCompDllPath != null)
			{
				return cachedReaCompDllPath;
			}
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string[] array = new string[7]
			{
				"C:\\Program Files\\DolphinXVDO\\engine\\VSTPlugins\\reacomp-standalone.dll",
				"C:\\Program Files\\DolphinXVDO\\plugins\\reacomp-standalone.dll",
				Path.Combine(baseDirectory, "engine", "VSTPlugins", "reacomp-standalone.dll"),
				Path.Combine(baseDirectory, "plugins", "reacomp-standalone.dll"),
				Path.Combine(baseDirectory, "reacomp-standalone.dll"),
				"C:\\Program Files\\EqualizerAPO\\VSTPlugins\\reacomp-standalone.dll",
				"C:\\Program Files\\EqualizerAPO\\plugins\\reacomp-standalone.dll"
			};
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (File.Exists(text))
				{
					cachedReaCompDllPath = text.Replace("/", "\\");
					return cachedReaCompDllPath;
				}
			}
			cachedReaCompDllPath = "reacomp-standalone.dll";
			return cachedReaCompDllPath;
		}

		public void SyncToAPO()
		{
			isPeakGainDirty = true;
			SaveDedicatedState();
			SyncAudioConfigOnly();
		}

		private void EnsureTargetConfigPaths()
		{
			if (cachedTargetConfigPaths != null) return;
			cachedTargetConfigPaths = new List<string>();
			try
			{
				string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
				string baseConfigDir = Path.Combine(baseDirectory, "config");
				if (!Directory.Exists(baseConfigDir))
				{
					Directory.CreateDirectory(baseConfigDir);
				}
				cachedTargetConfigPaths.Add(Path.Combine(baseConfigDir, "config.txt"));

				string[] candidateDirs = new string[]
				{
					"C:\\Program Files\\DolphinXVDO\\config",
					"C:\\Program Files\\EqualizerAPO\\config"
				};
				foreach (string dir in candidateDirs)
				{
					if (Directory.Exists(dir))
					{
						cachedTargetConfigPaths.Add(Path.Combine(dir, "config.txt"));
					}
				}
			}
			catch
			{
			}
		}

		public void SyncAudioConfigOnly()
		{
			Stopwatch swInit = Stopwatch.StartNew();
			try
			{
				string reaCompDllPath = GetReaCompDllPath();
				StringWriter stringWriter = new StringWriter();
				stringWriter.WriteLine("# ========================================================");
				stringWriter.WriteLine("# DolphinXvdo v2.3");
				stringWriter.WriteLine("# ========================================================");
				if (masterBypass)
				{
					stringWriter.WriteLine("# DOLPHINXVDO_BYPASS_ALL: ON");
					stringWriter.WriteLine("Preamp: 0.0 dB");
					stringWriter.WriteLine();
					stringWriter.WriteLine("# --- 15-Band Parametric EQ (Master Bypassed) ---");
					for (int i = 0; i < filters.Count; i++)
					{
						BiquadFilter biquadFilter = filters[i];
						stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "# Filter {0}: ON {1} Fc {2:0} Hz Gain {3:0.0} dB Q {4:0.00}", i + 1, biquadFilter.Type, biquadFilter.Frequency, biquadFilter.Gain, biquadFilter.Q));
					}
					if (chkCompPower.Checked)
					{
						stringWriter.WriteLine();
						stringWriter.WriteLine("# --- ReaComp VST Compressor (Master Bypassed) ---");
						double num = Math.Pow(10.0, sliderThresh.Value / 20.0);
						double num2 = Math.Max(0.0, Math.Min(1.0, (CompRatio - 1.0) / 100.0));
						double num3 = Math.Max(0.0, Math.Min(1.0, CompAttack / 500.0));
						double num4 = Math.Max(0.0, Math.Min(1.0, CompRelease / 5000.0));
						double num5 = Math.Max(0.0, Math.Min(1.0, CompKnee / 24.0));
						double num6 = Math.Pow(10.0, CompWet / 20.0);
						double num7 = ((CompDry <= -59.5) ? 0.0 : Math.Pow(10.0, CompDry / 20.0));
						double num8 = Math.Max(0.0, Math.Min(1.0, CompPreComp / 250.0));
						double num9 = (CompAutoMakeup ? 1.0 : 0.0);
						double num10 = Math.Max(0.0, Math.Min(1.0, CompRms / 1000.0));
						double num11 = (CompAutoRelease ? 1.0 : 0.0);
						double num12 = (CompClassicAttack ? 1.0 : 0.0);
						stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "# VSTPlugin: Library \"{0}\" Thresh {1:0.00000} Ratio {2:0.00000} Attack {3:0.00000} Release {4:0.00000} Knee {5:0.00000} Wet {6:0.00000} Dry {7:0.00000} \"Pre-comp\" {8:0.00000} AutoMkUp {9:0.0} \"RMS size\" {10:0.00000} AutoRel {11:0.0} ClsAttk {12:0.0}", reaCompDllPath, num, num2, num3, num4, num5, num6, num7, num8, num9, num10, num11, num12));
					}
					if (CustomVstPlugins != null && CustomVstPlugins.Count > 0)
					{
						bool hasCustom = false;
						for (int k = 0; k < CustomVstPlugins.Count; k++)
						{
							if (CustomVstPlugins[k].Enabled && File.Exists(CustomVstPlugins[k].FilePath))
							{
								if (!hasCustom)
								{
									stringWriter.WriteLine();
									stringWriter.WriteLine("# --- Custom VST Plugins (Master Bypassed) ---");
									hasCustom = true;
								}
								if (!string.IsNullOrEmpty(CustomVstPlugins[k].ChunkData))
								{
									stringWriter.WriteLine(string.Format("# VSTPlugin: Library \"{0}\" ChunkData \"{1}\"", CustomVstPlugins[k].FilePath, CustomVstPlugins[k].ChunkData));
								}
								else
								{
									stringWriter.WriteLine(string.Format("# VSTPlugin: Library \"{0}\"", CustomVstPlugins[k].FilePath));
								}
							}
						}
					}
				}
				else
				{
					stringWriter.WriteLine("# DOLPHINXVDO_BYPASS_ALL: OFF");
					stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "Preamp: {0:0.0} dB", preampDb));
					stringWriter.WriteLine();
					if (btnEqPower != null && !btnEqPower.Checked)
					{
						stringWriter.WriteLine("# --- 15-Band Parametric EQ (Bypassed) ---");
						for (int i = 0; i < filters.Count; i++)
						{
							BiquadFilter biquadFilter = filters[i];
							stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "# Filter {0}: ON {1} Fc {2:0} Hz Gain {3:0.0} dB Q {4:0.00}", i + 1, biquadFilter.Type, biquadFilter.Frequency, biquadFilter.Gain, biquadFilter.Q));
						}
					}
					else
					{
						stringWriter.WriteLine("# --- 15-Band Parametric EQ ---");
						for (int i = 0; i < filters.Count; i++)
						{
							BiquadFilter biquadFilter = filters[i];
							stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "Filter {0}: ON {1} Fc {2:0} Hz Gain {3:0.0} dB Q {4:0.00}", i + 1, biquadFilter.Type, biquadFilter.Frequency, biquadFilter.Gain, biquadFilter.Q));
						}
					}
					if (chkCompPower.Checked)
					{
						stringWriter.WriteLine();
						stringWriter.WriteLine("# --- ReaComp VST Compressor ---");
						double num = Math.Pow(10.0, sliderThresh.Value / 20.0);
						double num2 = Math.Max(0.0, Math.Min(1.0, (CompRatio - 1.0) / 100.0));
						double num3 = Math.Max(0.0, Math.Min(1.0, CompAttack / 500.0));
						double num4 = Math.Max(0.0, Math.Min(1.0, CompRelease / 5000.0));
						double num5 = Math.Max(0.0, Math.Min(1.0, CompKnee / 24.0));
						double num6 = Math.Pow(10.0, CompWet / 20.0);
						double num7 = ((CompDry <= -59.5) ? 0.0 : Math.Pow(10.0, CompDry / 20.0));
						double num8 = Math.Max(0.0, Math.Min(1.0, CompPreComp / 250.0));
						double num9 = (CompAutoMakeup ? 1.0 : 0.0);
						double num10 = Math.Max(0.0, Math.Min(1.0, CompRms / 1000.0));
						double num11 = (CompAutoRelease ? 1.0 : 0.0);
						double num12 = (CompClassicAttack ? 1.0 : 0.0);
						stringWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "VSTPlugin: Library \"{0}\" Thresh {1:0.00000} Ratio {2:0.00000} Attack {3:0.00000} Release {4:0.00000} Knee {5:0.00000} Wet {6:0.00000} Dry {7:0.00000} \"Pre-comp\" {8:0.00000} AutoMkUp {9:0.0} \"RMS size\" {10:0.00000} AutoRel {11:0.0} ClsAttk {12:0.0}", reaCompDllPath, num, num2, num3, num4, num5, num6, num7, num8, num9, num10, num11, num12));
					}
					if (CustomVstPlugins != null && CustomVstPlugins.Count > 0)
					{
						bool hasCustom = false;
						for (int k = 0; k < CustomVstPlugins.Count; k++)
						{
							if (CustomVstPlugins[k].Enabled && File.Exists(CustomVstPlugins[k].FilePath))
							{
								if (!hasCustom)
								{
									stringWriter.WriteLine();
									stringWriter.WriteLine("# --- Custom VST Plugins ---");
									hasCustom = true;
								}
								if (!string.IsNullOrEmpty(CustomVstPlugins[k].ChunkData))
								{
									stringWriter.WriteLine(string.Format("VSTPlugin: Library \"{0}\" ChunkData \"{1}\"", CustomVstPlugins[k].FilePath, CustomVstPlugins[k].ChunkData));
								}
								else
								{
									stringWriter.WriteLine(string.Format("VSTPlugin: Library \"{0}\"", CustomVstPlugins[k].FilePath));
								}
							}
						}
					}
				}
				string contents = stringWriter.ToString();
				if (contents == lastSyncedConfig) return;
				lastSyncedConfig = contents;

				EnsureTargetConfigPaths();
				for (int j = 0; j < cachedTargetConfigPaths.Count; j++)
				{
					try
					{
						File.WriteAllText(cachedTargetConfigPaths[j], contents);
					}
					catch
					{
					}
				}
				swInit.Stop();
				lastMeasuredInitTimeMs = Math.Max(0.5, swInit.Elapsed.TotalMilliseconds);
				UpdateEstimatedProperties();
			}
			catch
			{
				swInit.Stop();
			}
		}

		public double CalculateRealPeakGain()
		{
			if (!isPeakGainDirty)
			{
				return cachedPeakGain;
			}
			if (masterBypass)
			{
				cachedPeakGain = 0.0;
				isPeakGainDirty = false;
				return 0.0;
			}
			double peak = preampDb;
			bool eqActive = (btnEqPower == null || btnEqPower.Checked);
			if (!eqActive || filters == null || filters.Count == 0)
			{
				cachedPeakGain = peak;
				isPeakGainDirty = false;
				return peak;
			}

			// Sample 150 points logarithmically across human audible hearing range 20 Hz to 20,000 Hz
			const int SAMPLES = 150;
			double logMin = Math.Log10(20.0);
			double logMax = Math.Log10(20000.0);
			double step = (logMax - logMin) / (SAMPLES - 1);

			for (int i = 0; i < SAMPLES; i++)
			{
				double freq = Math.Pow(10.0, logMin + i * step);
				double resp = preampDb;
				for (int f = 0; f < filters.Count; f++)
				{
					if (filters[f] != null)
					{
						resp += filters[f].GetMagnitudeDbAt(freq);
					}
				}
				if (resp > peak)
				{
					peak = resp;
				}
			}
			cachedPeakGain = peak;
			isPeakGainDirty = false;
			return peak;
		}

		public double CalculateRealLatencyMs(out int latencySamples)
		{
			double ms = 0.0;
			if (!masterBypass && chkCompPower != null && chkCompPower.Checked && CompPreComp > 0.0)
			{
				ms = CompPreComp;
			}
			latencySamples = (int)Math.Round(ms * 48.0);
			return ms;
		}

		public void UpdateEstimatedProperties()
		{
			try
			{
				double peakGain = CalculateRealPeakGain();
				string peakStr;
				if (peakGain > 0.05)
				{
					peakStr = string.Format(CultureInfo.InvariantCulture, "+{0:0.0} dB", peakGain);
				}
				else
				{
					peakStr = string.Format(CultureInfo.InvariantCulture, "{0:0.0} dB", peakGain);
				}

				int latencySamples;
				double latencyMs = CalculateRealLatencyMs(out latencySamples);
				string latencyStr = string.Format(CultureInfo.InvariantCulture, "{0:0.0} ms ({1} s.)", latencyMs, latencySamples);

				string initTimeStr = string.Format(CultureInfo.InvariantCulture, "{0:0.0} ms", lastMeasuredInitTimeMs);

				string cpuStr = string.Format(CultureInfo.InvariantCulture, "{0:0.0} % (one core)", Math.Max(0.1, lastCalculatedCoreCpu));

				if (lblEstPeakGain != null && !lblEstPeakGain.IsDisposed)
				{
					lblEstPeakGain.Text = peakStr;
					lblEstPeakGain.ForeColor = (peakGain > 0.05) ? UiTheme.AccentBlue : UiTheme.AccentGreen;
				}
				if (lblEstLatency != null && !lblEstLatency.IsDisposed)
				{
					lblEstLatency.Text = latencyStr;
				}
				if (lblEstInitTime != null && !lblEstInitTime.IsDisposed)
				{
					lblEstInitTime.Text = initTimeStr;
				}
				if (lblEstCpuUsage != null && !lblEstCpuUsage.IsDisposed)
				{
					lblEstCpuUsage.Text = cpuStr;
					lblEstCpuUsage.ForeColor = (lastCalculatedCoreCpu >= 20.0) ? UiTheme.AccentAlert : ((lastCalculatedCoreCpu >= 10.0) ? UiTheme.AccentBlue : UiTheme.TextBody);
				}

				if (btnAutoPreamp != null && !btnAutoPreamp.IsDisposed)
				{
					if (peakGain > 0.05)
					{
						btnAutoPreamp.NormalBorderColor = UiTheme.AccentBlue;
						btnAutoPreamp.HoverBorderColor = UiTheme.TextPrimary;
						btnAutoPreamp.NormalBackColor = Color.FromArgb(16, 28, 40);
						btnAutoPreamp.HoverBackColor = Color.FromArgb(22, 38, 56);
						btnAutoPreamp.TextColor = UiTheme.AccentBlue;
						btnAutoPreamp.HoverTextColor = UiTheme.TextPrimary;
						btnAutoPreamp.Text = string.Format(CultureInfo.InvariantCulture, "Auto (-{0:0.0}dB)", peakGain);
					}
					else
					{
						btnAutoPreamp.NormalBorderColor = UiTheme.BorderRing;
						btnAutoPreamp.HoverBorderColor = UiTheme.AccentBlue;
						btnAutoPreamp.NormalBackColor = UiTheme.BgWell;
						btnAutoPreamp.HoverBackColor = UiTheme.BgChip;
						btnAutoPreamp.TextColor = UiTheme.TextMuted;
						btnAutoPreamp.HoverTextColor = UiTheme.TextPrimary;
						btnAutoPreamp.Text = "Auto Preamp";
					}
					btnAutoPreamp.Invalidate();
				}

				if (lblStatusUsage != null && !lblStatusUsage.IsDisposed)
				{
					lblStatusUsage.Text = string.Format("● Estimated: Peak: {0}  |  Latency: {1}  |  Init: {2}  |  CPU: {3}", peakStr, latencyStr, initTimeStr, cpuStr);
					lblStatusUsage.ForeColor = ((peakGain > 0.05) ? UiTheme.TextBody : UiTheme.TextMuted);
				}
			}
			catch
			{
			}
		}

		private void ApplyAutoPreamp()
		{
			try
			{
				double peak = CalculateRealPeakGain();
				if (peak > 0.0)
				{
					double targetPreamp = Math.Round(preampDb - peak, 1);
					targetPreamp = Math.Max(-30.0, Math.Min(30.0, targetPreamp));
					preampDb = targetPreamp;
					sliderPreamp.Value = targetPreamp;
					txtPreampVal.Text = string.Format("{0:+0.0;-0.0;0.0}", preampDb);
					txtPreampVal.ForeColor = ((preampDb > 0.0) ? UiTheme.AccentGreen : ((preampDb < 0.0) ? UiTheme.AccentBlue : UiTheme.TextPrimary));
					isPeakGainDirty = true;
					pnlGraph.Invalidate();
					TriggerSync(true);
					UpdateEstimatedProperties();
				}
			}
			catch
			{
			}
		}

		private void UpdateUsageMetrics()
		{
			if (base.WindowState == FormWindowState.Minimized || !base.Visible)
			{
				return;
			}
			try
			{
				double dspLoadBenchmark = 0.2;
				if (!masterBypass)
				{
					if (btnEqPower == null || btnEqPower.Checked)
					{
						int activeFilters = (filters != null && filters.Count > 0) ? filters.Count : 10;
						dspLoadBenchmark += activeFilters * 0.10; // 10 filters = 1.0% + base 0.2% = 1.2% constant
					}
					if (chkCompPower != null && chkCompPower.Checked)
					{
						dspLoadBenchmark += 0.35;
					}
					if (CustomVstPlugins != null)
					{
						for (int v = 0; v < CustomVstPlugins.Count; v++)
						{
							if (CustomVstPlugins[v].Enabled)
							{
								dspLoadBenchmark += 0.40;
							}
						}
					}
				}
				else
				{
					dspLoadBenchmark = 0.0;
				}
				lastCalculatedCoreCpu = Math.Round(dspLoadBenchmark, 1);

				UpdateEstimatedProperties();
			}
			catch
			{
			}
		}

		private ModernAudioDevicesDialog activeAudioDevicesDialog = null;

		public void OpenModernAudioDevicesDialog()
		{
			if (activeAudioDevicesDialog != null && !activeAudioDevicesDialog.IsDisposed)
			{
				if (activeAudioDevicesDialog.WindowState == FormWindowState.Minimized)
				{
					activeAudioDevicesDialog.WindowState = FormWindowState.Normal;
				}
				activeAudioDevicesDialog.BringToFront();
				activeAudioDevicesDialog.Activate();
				return;
			}
			activeAudioDevicesDialog = new ModernAudioDevicesDialog(this);
			activeAudioDevicesDialog.FormClosed += delegate
			{
				activeAudioDevicesDialog = null;
			};
			activeAudioDevicesDialog.Show(this);
		}

		public void UpdateVstRackButtonText()
		{
			if (btnVstRack != null && !btnVstRack.IsDisposed)
			{
				btnVstRack.Text = "VST Rack";
			}
		}

		private VstRackDialog activeVstRackDialog = null;

		private void OpenVstRackDialog()
		{
			if (activeVstRackDialog != null && !activeVstRackDialog.IsDisposed)
			{
				if (activeVstRackDialog.WindowState == FormWindowState.Minimized)
				{
					activeVstRackDialog.WindowState = FormWindowState.Normal;
				}
				activeVstRackDialog.BringToFront();
				activeVstRackDialog.Activate();
				return;
			}
			activeVstRackDialog = new VstRackDialog(this);
			activeVstRackDialog.FormClosed += delegate
			{
				activeVstRackDialog = null;
				UpdateVstRackButtonText();
				TriggerSync(true);
				SaveDedicatedState();
			};
			activeVstRackDialog.Show(this);
		}
	}
	public class DarkMenuColorTable : ProfessionalColorTable
	{
		public override Color MenuItemSelected
		{
			get
			{
				return UiTheme.BgChip;
			}
		}

		public override Color MenuItemSelectedGradientBegin
		{
			get
			{
				return UiTheme.BgChip;
			}
		}

		public override Color MenuItemSelectedGradientEnd
		{
			get
			{
				return UiTheme.BgChip;
			}
		}

		public override Color MenuItemBorder
		{
			get
			{
				return UiTheme.AccentBlue;
			}
		}

		public override Color MenuBorder
		{
			get
			{
				return UiTheme.BorderRingSolid;
			}
		}

		public override Color ToolStripDropDownBackground
		{
			get
			{
				return UiTheme.BgCard;
			}
		}

		public override Color ImageMarginGradientBegin
		{
			get
			{
				return UiTheme.BgCard;
			}
		}

		public override Color ImageMarginGradientMiddle
		{
			get
			{
				return UiTheme.BgCard;
			}
		}

		public override Color ImageMarginGradientEnd
		{
			get
			{
				return UiTheme.BgCard;
			}
		}

		public override Color SeparatorDark
		{
			get
			{
				return UiTheme.BorderRingSolid;
			}
		}

		public override Color SeparatorLight
		{
			get
			{
				return UiTheme.BorderRingSolid;
			}
		}

		public override Color CheckBackground
		{
			get
			{
				return UiTheme.BgChip;
			}
		}

		public override Color CheckSelectedBackground
		{
			get
			{
				return UiTheme.AccentBlue;
			}
		}

		public override Color CheckPressedBackground
		{
			get
			{
				return UiTheme.AccentBlue;
			}
		}
	}
	public class AdvancedCompDialog : Form
	{
		private MainForm parent;

		private ModernHorizontalSlider sRatio;

		private ModernHorizontalSlider sWet;

		private ModernHorizontalSlider sDry;

		private ModernHorizontalSlider sKnee;

		private ModernHorizontalSlider sAttack;

		private ModernHorizontalSlider sRelease;

		private ModernHorizontalSlider sPreComp;

		private ModernHorizontalSlider sRms;

		private TextBox txtRatio;

		private TextBox txtWet;

		private TextBox txtDry;

		private TextBox txtKnee;

		private TextBox txtAttack;

		private TextBox txtRelease;

		private TextBox txtPreComp;

		private TextBox txtRms;

		private CheckBox chkClsAttk;

		private CheckBox chkAutoRel;

		private CheckBox chkAutoMk;

		public AdvancedCompDialog(MainForm main)
		{
			parent = main;
			Text = "Advanced ReaComp VST Settings - DolphinXvdo v2.3";
			try
			{
				if (main != null && main.Icon != null)
				{
					base.Icon = main.Icon;
				}
			}
			catch
			{
			}
			base.Size = new Size(580, 680);
			base.StartPosition = FormStartPosition.CenterParent;
			BackColor = UiTheme.BgCanvas;
			ForeColor = UiTheme.TextBody;
			Font = UiTheme.DisplayFont(9f);
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			InitializeUI();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			MainForm.ApplyDiscordDarkStyle(base.Handle);
		}

		private void InitializeUI()
		{
			Panel panel2 = new Panel
			{
				Dock = DockStyle.Top,
				Height = 58,
				BackColor = UiTheme.BgCard,
				Padding = new Padding(16, 10, 16, 8)
			};
			panel2.Paint += delegate(object s, PaintEventArgs pe)
			{
				using (Pen pen = new Pen(UiTheme.BorderSubtle, 1f))
				{
					pe.Graphics.DrawLine(pen, 0, panel2.Height - 1, panel2.Width, panel2.Height - 1);
				}
			};
			Label value = new Label
			{
				Text = "Advanced VST Compressor Parameters",
				Font = UiTheme.DisplayFont(11f, FontStyle.Bold),
				ForeColor = UiTheme.TextPrimary,
				AutoSize = true,
				Location = new Point(14, 8)
			};
			Label value2 = new Label
			{
				Text = "Fine-tune all internal DSP parameters for ReaComp VST",
				ForeColor = UiTheme.TextMuted,
				AutoSize = true,
				Location = new Point(15, 31),
				Font = UiTheme.DisplayFont(8f)
			};
			panel2.Controls.Add(value);
			panel2.Controls.Add(value2);
			base.Controls.Add(panel2);
			Panel panel4 = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 54,
				BackColor = UiTheme.BgCard,
				Padding = new Padding(12)
			};
			panel4.Paint += delegate(object s, PaintEventArgs pe)
			{
				using (Pen pen = new Pen(UiTheme.BorderSubtle, 1f))
				{
					pe.Graphics.DrawLine(pen, 0, 0, panel4.Width, 0);
				}
			};
			ModernActionButton button2 = new ModernActionButton
			{
				Text = "Apply & Close",
				CornerRadius = 6,
				NormalBackColor = UiTheme.CtaBg,
				HoverBackColor = UiTheme.CtaHoverBg,
				PressedBackColor = UiTheme.CtaPressedBg,
				NormalBorderColor = UiTheme.CtaBg,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.CtaText,
				HoverTextColor = UiTheme.CtaText,
				Location = new Point(408, 11),
				Width = 140,
				Height = 32,
				Font = UiTheme.DisplayFont(9f, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			button2.Click += delegate
			{
				base.DialogResult = DialogResult.OK;
				Close();
			};
			panel4.Controls.Add(button2);
			base.Controls.Add(panel4);
			Panel panel6 = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = UiTheme.BgCanvas,
				Padding = new Padding(16, 12, 16, 12),
				AutoScroll = true
			};
			base.Controls.Add(panel6);
			panel6.BringToFront();
			TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
			{
				Dock = DockStyle.Top,
				ColumnCount = 1,
				RowCount = 9,
				AutoSize = true,
				BackColor = Color.Transparent
			};
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			for (int i = 0; i < 8; i++)
			{
				tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
			}
			tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
			tableLayoutPanel2.Controls.Add(CreateRow("Ratio:", " : 1", 1.0, 100.0, parent.CompRatio, 1, out sRatio, out txtRatio, delegate(double v)
			{
				parent.CompRatio = v;
			}), 0, 0);
			tableLayoutPanel2.Controls.Add(CreateRow("Attack Time:", " ms", 0.0, 500.0, parent.CompAttack, 1, out sAttack, out txtAttack, delegate(double v)
			{
				parent.CompAttack = v;
			}), 0, 1);
			tableLayoutPanel2.Controls.Add(CreateRow("Release Time:", " ms", 0.0, 5000.0, parent.CompRelease, 0, out sRelease, out txtRelease, delegate(double v)
			{
				parent.CompRelease = v;
			}), 0, 2);
			tableLayoutPanel2.Controls.Add(CreateRow("Pre-comp / Lookahead:", " ms", 0.0, 250.0, parent.CompPreComp, 1, out sPreComp, out txtPreComp, delegate(double v)
			{
				parent.CompPreComp = v;
			}), 0, 3);
			tableLayoutPanel2.Controls.Add(CreateRow("Soft Knee:", " dB", 0.0, 24.0, parent.CompKnee, 1, out sKnee, out txtKnee, delegate(double v)
			{
				parent.CompKnee = v;
			}), 0, 4);
			tableLayoutPanel2.Controls.Add(CreateRow("RMS Size:", " ms", 0.0, 1000.0, parent.CompRms, 1, out sRms, out txtRms, delegate(double v)
			{
				parent.CompRms = v;
			}), 0, 5);
			tableLayoutPanel2.Controls.Add(CreateRow("Wet Output:", " dB", -60.0, 24.0, parent.CompWet, 1, out sWet, out txtWet, delegate(double v)
			{
				parent.CompWet = v;
			}), 0, 6);
			tableLayoutPanel2.Controls.Add(CreateRow("Dry Output:", " dB", -60.0, 24.0, parent.CompDry, 1, out sDry, out txtDry, delegate(double v)
			{
				parent.CompDry = v;
			}), 0, 7);
			Panel panel8 = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.Transparent
			};
			chkClsAttk = new CheckBox
			{
				Text = "Classic Attack",
				Checked = parent.CompClassicAttack,
				ForeColor = UiTheme.TextBody,
				Font = UiTheme.DisplayFont(9f, FontStyle.Bold),
				AutoSize = true,
				Location = new Point(10, 10)
			};
			chkClsAttk.CheckedChanged += delegate
			{
				parent.CompClassicAttack = chkClsAttk.Checked;
			};
			panel8.Controls.Add(chkClsAttk);
			chkAutoRel = new CheckBox
			{
				Text = "Auto Release",
				Checked = parent.CompAutoRelease,
				ForeColor = UiTheme.TextBody,
				Font = UiTheme.DisplayFont(9f, FontStyle.Bold),
				AutoSize = true,
				Location = new Point(175, 10)
			};
			chkAutoRel.CheckedChanged += delegate
			{
				parent.CompAutoRelease = chkAutoRel.Checked;
			};
			panel8.Controls.Add(chkAutoRel);
			chkAutoMk = new CheckBox
			{
				Text = "Auto Make-up",
				Checked = parent.CompAutoMakeup,
				ForeColor = UiTheme.TextBody,
				Font = UiTheme.DisplayFont(9f, FontStyle.Bold),
				AutoSize = true,
				Location = new Point(330, 10)
			};
			chkAutoMk.CheckedChanged += delegate
			{
				parent.CompAutoMakeup = chkAutoMk.Checked;
			};
			panel8.Controls.Add(chkAutoMk);
			tableLayoutPanel2.Controls.Add(panel8, 0, 8);
			panel6.Controls.Add(tableLayoutPanel2);
		}

		private Panel CreateRow(string title, string unit, double min, double max, double curVal, int decimals, out ModernHorizontalSlider slider, out TextBox txtBox, Action<double> onValChanged)
		{
			Panel panel2 = new Panel
			{
				Dock = DockStyle.Fill,
				Height = 56,
				BackColor = Color.Transparent,
				Padding = new Padding(2, 2, 2, 4)
			};
			Panel panel4 = new Panel
			{
				Dock = DockStyle.Top,
				Height = 24,
				BackColor = Color.Transparent
			};
			Label value = new Label
			{
				Text = title,
				ForeColor = UiTheme.TextBody,
				Font = UiTheme.DisplayFont(8.5f, FontStyle.Bold),
				Dock = DockStyle.Left,
				AutoSize = true,
				TextAlign = ContentAlignment.MiddleLeft
			};
			Label value2 = new Label
			{
				Text = unit,
				ForeColor = UiTheme.TextMuted,
				Font = UiTheme.MonoFont(8.2f),
				Dock = DockStyle.Right,
				Width = 38,
				TextAlign = ContentAlignment.MiddleLeft
			};
			string fmt = ((decimals == 0) ? "0" : "0.0");
			TextBox tb = new TextBox
			{
				Text = curVal.ToString(fmt, CultureInfo.InvariantCulture),
				ForeColor = UiTheme.AccentBlue,
				Font = UiTheme.MonoFont(8.8f, FontStyle.Bold),
				TextAlign = HorizontalAlignment.Center
			};
			ModernNumberBox tbWrap = new ModernNumberBox(tb, 5)
			{
				Dock = DockStyle.Right,
				Width = 68
			};
			panel4.Controls.Add(value);
			panel4.Controls.Add(tbWrap);
			panel4.Controls.Add(value2);
			ModernHorizontalSlider sl = new ModernHorizontalSlider
			{
				Dock = DockStyle.Bottom,
				Height = 24,
				MinValue = min,
				MaxValue = max,
				DecimalPlaces = decimals,
				Value = curVal
			};
			bool updating = false;
			sl.ValueChanged += delegate
			{
				if (!updating)
				{
					updating = true;
					tb.Text = sl.Value.ToString(fmt, CultureInfo.InvariantCulture);
					onValChanged(sl.Value);
					updating = false;
				}
			};
			Action commitText = delegate
			{
				double result;
				if (double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
				{
					updating = true;
					result = Math.Max(min, Math.Min(max, result));
					sl.Value = result;
					tb.Text = result.ToString(fmt, CultureInfo.InvariantCulture);
					onValChanged(result);
					updating = false;
				}
			};
			tb.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.KeyCode == Keys.Return)
				{
					commitText();
					e.SuppressKeyPress = true;
				}
			};
			tb.Leave += delegate
			{
				commitText();
			};
			slider = sl;
			txtBox = tb;
			panel2.Controls.Add(panel4);
			panel2.Controls.Add(sl);
			return panel2;
		}
	}

	public class CustomVstItem
	{
		public string Name { get; set; }
		public string FilePath { get; set; }
		public bool Enabled { get; set; }
		public string ChunkData { get; set; }

		public CustomVstItem()
		{
			Enabled = true;
			ChunkData = "";
		}

		public CustomVstItem(string name, string filePath, bool enabled = true, string chunkData = "")
		{
			Name = name;
			FilePath = filePath;
			Enabled = enabled;
			ChunkData = chunkData ?? "";
		}
	}

	public class AudioDeviceInfo
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Adapter { get; set; }
		public string Type { get; set; }
		public string RenderKey { get; set; }
		public bool Installed { get; set; }
	}

	public class ModernAudioDevicesDialog : Form
	{
		private MainForm main;
		private Panel pnlDeviceList;
		private Label lblTabPlayback;
		private Label lblTabRecording;
		private string currentTab = "Playback";
		private List<AudioDeviceInfo> cachedDevices = new List<AudioDeviceInfo>();

		public ModernAudioDevicesDialog(MainForm parent)
		{
			main = parent;
			try
			{
				if (main != null && main.Icon != null)
				{
					base.Icon = main.Icon;
				}
			}
			catch { }

			base.Text = "Audio Devices Setup - DolphinXvdo v2.3";
			base.Size = new Size(720, 560);
			base.StartPosition = FormStartPosition.CenterParent;
			BackColor = UiTheme.BgCanvas;
			ForeColor = UiTheme.TextBody;
			Font = UiTheme.DisplayFont(9f);
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = true;

			InitializeUI();
			RefreshDeviceList();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			MainForm.ApplyDiscordDarkStyle(base.Handle);
		}

		private void InitializeUI()
		{
			// Header
			Panel pnlHeader = new Panel
			{
				Dock = DockStyle.Top,
				Height = 74,
				BackColor = UiTheme.BgCard,
				Padding = new Padding(18, 12, 18, 8)
			};
			Label lblTitle = new Label
			{
				Text = "Audio Devices Setup (APO Hook)",
				Font = UiTheme.DisplayFont(12f, FontStyle.Bold),
				ForeColor = UiTheme.TextPrimary,
				Location = new Point(16, 10),
				AutoSize = true
			};
			Label lblSub = new Label
			{
				Text = "Configure Equalizer APO on your Windows Playback & Recording audio endpoints",
				ForeColor = UiTheme.TextMuted,
				Location = new Point(17, 36),
				AutoSize = true,
				Font = UiTheme.DisplayFont(8.5f)
			};
			pnlHeader.Controls.Add(lblTitle);
			pnlHeader.Controls.Add(lblSub);

			// Tabs Panel
			Panel pnlTabs = new Panel
			{
				Dock = DockStyle.Top,
				Height = 40,
				BackColor = UiTheme.BgCanvas,
				Padding = new Padding(18, 4, 18, 0)
			};

			lblTabPlayback = new Label
			{
				Text = "Playback Devices",
				Font = UiTheme.DisplayFont(9.2f, FontStyle.Bold),
				ForeColor = UiTheme.AccentBlue,
				BackColor = UiTheme.BgChip,
				Size = new Size(156, 32),
				TextAlign = ContentAlignment.MiddleCenter,
				Cursor = Cursors.Hand,
				Location = new Point(18, 4)
			};
			lblTabPlayback.Click += delegate
			{
				currentTab = "Playback";
				UpdateTabStyles();
				RenderDevices();
			};

			lblTabRecording = new Label
			{
				Text = "Recording Devices",
				Font = UiTheme.DisplayFont(9.2f),
				ForeColor = UiTheme.TextMuted,
				BackColor = Color.Transparent,
				Size = new Size(156, 32),
				TextAlign = ContentAlignment.MiddleCenter,
				Cursor = Cursors.Hand,
				Location = new Point(182, 4)
			};
			lblTabRecording.Click += delegate
			{
				currentTab = "Recording";
				UpdateTabStyles();
				RenderDevices();
			};

			pnlTabs.Controls.Add(lblTabPlayback);
			pnlTabs.Controls.Add(lblTabRecording);

			// Device list scroll panel
			pnlDeviceList = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				Padding = new Padding(18, 12, 18, 12),
				BackColor = UiTheme.BgCanvas
			};

			// Bottom Panel
			Panel pnlBottom = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 56,
				BackColor = UiTheme.BgCard,
				Padding = new Padding(18, 10, 18, 10)
			};

			ModernActionButton btnRestartAudio = new ModernActionButton
			{
				Text = "Restart Audio Engine",
				Size = new Size(160, 34),
				Location = new Point(18, 11),
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.AccentBlue,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.DisplayFont(8.5f, FontStyle.Bold)
			};
			btnRestartAudio.Click += delegate
			{
				btnRestartAudio.Text = "Restarting...";
				btnRestartAudio.Enabled = false;
				ThreadPool.QueueUserWorkItem(delegate
				{
					RestartAudioEngine();
					if (!IsDisposed && IsHandleCreated)
					{
						BeginInvoke((MethodInvoker)delegate
						{
							btnRestartAudio.Text = "Restart Audio Engine";
							btnRestartAudio.Enabled = true;
							MessageBox.Show(this, "Windows Audio Engine restarted successfully!", "Audio Service", MessageBoxButtons.OK, MessageBoxIcon.Information);
						});
					}
				});
			};
			pnlBottom.Controls.Add(btnRestartAudio);

			ModernActionButton btnClassic = new ModernActionButton
			{
				Text = "Classic Setup (DeviceSelector.exe)",
				Size = new Size(220, 34),
				Location = new Point(188, 11),
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentAlert,
				TextColor = UiTheme.TextMuted,
				HoverTextColor = UiTheme.TextPrimary,
				Font = UiTheme.DisplayFont(8.5f)
			};
			btnClassic.Click += delegate
			{
				if (main != null) main.LaunchOfficialDeviceSelector();
			};
			pnlBottom.Controls.Add(btnClassic);

			ModernActionButton btnClose = new ModernActionButton
			{
				Text = "Done",
				Size = new Size(92, 34),
				Anchor = (AnchorStyles.Top | AnchorStyles.Right),
				Location = new Point(pnlBottom.Width - 110, 11),
				CornerRadius = 6,
				NormalBackColor = UiTheme.CtaBg,
				HoverBackColor = UiTheme.CtaHoverBg,
				PressedBackColor = UiTheme.CtaPressedBg,
				NormalBorderColor = UiTheme.CtaBg,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.CtaText,
				HoverTextColor = UiTheme.CtaText,
				Font = UiTheme.DisplayFont(9f, FontStyle.Bold)
			};
			btnClose.Click += delegate
			{
				Close();
			};
			pnlBottom.Controls.Add(btnClose);

			base.Controls.Add(pnlDeviceList);
			base.Controls.Add(pnlTabs);
			base.Controls.Add(pnlHeader);
			base.Controls.Add(pnlBottom);
		}

		private void UpdateTabStyles()
		{
			if (currentTab == "Playback")
			{
				lblTabPlayback.ForeColor = UiTheme.AccentBlue;
				lblTabPlayback.BackColor = UiTheme.BgChip;
				lblTabPlayback.Font = UiTheme.DisplayFont(9.2f, FontStyle.Bold);

				lblTabRecording.ForeColor = UiTheme.TextMuted;
				lblTabRecording.BackColor = Color.Transparent;
				lblTabRecording.Font = UiTheme.DisplayFont(9.2f);
			}
			else
			{
				lblTabRecording.ForeColor = UiTheme.AccentBlue;
				lblTabRecording.BackColor = UiTheme.BgChip;
				lblTabRecording.Font = UiTheme.DisplayFont(9.2f, FontStyle.Bold);

				lblTabPlayback.ForeColor = UiTheme.TextMuted;
				lblTabPlayback.BackColor = Color.Transparent;
				lblTabPlayback.Font = UiTheme.DisplayFont(9.2f);
			}
		}

		private void RefreshDeviceList()
		{
			cachedDevices = QueryAudioDevicesDirect();
			RenderDevices();
		}

		public static List<AudioDeviceInfo> QueryAudioDevicesDirect()
		{
			List<AudioDeviceInfo> list = new List<AudioDeviceInfo>();
			string[] types = new string[] { "Render", "Capture" };
			string apoPreGuid = "{EACD2258-FCAC-4FF4-B36D-419E924A6D79}";
			string apoPostGuid = "{EC1CC9CE-FAED-4822-828A-82A81A6F018F}";
			string sfxKey = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5";
			string efxKey = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},7";
			string lfxKey = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},1";
			string gfxKey = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},2";

			foreach (string rk in types)
			{
				try
				{
					using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\" + rk))
					{
						if (baseKey == null) continue;
						string[] guids = baseKey.GetSubKeyNames();
						foreach (string g in guids)
						{
							try
							{
								using (RegistryKey devKey = baseKey.OpenSubKey(g))
								{
									if (devKey == null) continue;
									object stateObj = devKey.GetValue("DeviceState");
									if (stateObj == null) continue;
									int st = Convert.ToInt32(stateObj);
									if (st != 1) continue; // 1 = Active

									string name = "";
									string adapter = "";
									using (RegistryKey propKey = devKey.OpenSubKey("Properties"))
									{
										if (propKey != null)
										{
											object n = propKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2");
											if (n != null) name = n.ToString();
											object a = propKey.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6");
											if (a != null) adapter = a.ToString();
										}
									}

									bool installed = false;
									using (RegistryKey fxKey = devKey.OpenSubKey("FxProperties"))
									{
										if (fxKey != null)
										{
											object sfx = fxKey.GetValue(sfxKey);
											object efx = fxKey.GetValue(efxKey);
											object lfx = fxKey.GetValue(lfxKey);
											object gfx = fxKey.GetValue(gfxKey);
											if ((sfx != null && sfx.ToString().Equals(apoPreGuid, StringComparison.OrdinalIgnoreCase)) ||
												(efx != null && efx.ToString().Equals(apoPostGuid, StringComparison.OrdinalIgnoreCase)) ||
												(lfx != null && lfx.ToString().Equals(apoPreGuid, StringComparison.OrdinalIgnoreCase)) ||
												(gfx != null && gfx.ToString().Equals(apoPostGuid, StringComparison.OrdinalIgnoreCase)))
											{
												installed = true;
											}
										}
									}

									if (string.IsNullOrEmpty(name)) name = "Audio Endpoint";

									list.Add(new AudioDeviceInfo
									{
										Id = g,
										Name = name,
										Adapter = adapter,
										Type = (rk == "Render") ? "Playback" : "Recording",
										RenderKey = rk,
										Installed = installed
									});
								}
							}
							catch { }
						}
					}
				}
				catch { }
			}
			return list;
		}

		private void RenderDevices()
		{
			pnlDeviceList.SuspendLayout();
			pnlDeviceList.Controls.Clear();

			List<AudioDeviceInfo> filtered = cachedDevices.Where(d => d.Type == currentTab).ToList();
			if (filtered.Count == 0)
			{
				Label lblEmpty = new Label
				{
					Text = "No active " + currentTab + " audio endpoints found.",
					ForeColor = Color.FromArgb(140, 150, 160),
					Dock = DockStyle.Top,
					Height = 60,
					TextAlign = ContentAlignment.MiddleCenter,
					Font = new Font("Segoe UI", 10f)
				};
				pnlDeviceList.Controls.Add(lblEmpty);
				pnlDeviceList.ResumeLayout();
				return;
			}

			for (int i = filtered.Count - 1; i >= 0; i--)
			{
				AudioDeviceInfo dev = filtered[i];
				Panel card = CreateDeviceCard(dev);
				pnlDeviceList.Controls.Add(card);
			}

			pnlDeviceList.ResumeLayout();
		}

		private Panel CreateDeviceCard(AudioDeviceInfo dev)
		{
			DoubleBufferedPanel pnlCard = new DoubleBufferedPanel
			{
				Dock = DockStyle.Top,
				Height = 68,
				BackColor = UiTheme.BgCanvas,
				Margin = new Padding(0, 0, 0, 8),
				Padding = new Padding(14, 8, 14, 8)
			};

			pnlCard.Paint += delegate(object s, PaintEventArgs pe)
			{
				Color bColor = dev.Installed ? UiTheme.AccentBlue : UiTheme.BorderRing;
				UiTheme.DrawRoundedSurface(pe.Graphics, pnlCard.Width, pnlCard.Height, 7f, UiTheme.BgCard, bColor, 1f);
			};

			Label lblIcon = new Label
			{
				Text = (dev.Type == "Playback") ? "OUT" : "IN",
				Font = UiTheme.MonoFont(7.6f, FontStyle.Bold),
				ForeColor = dev.Installed ? UiTheme.AccentBlue : UiTheme.TextMuted,
				BackColor = UiTheme.BgChip,
				Location = new Point(14, 22),
				Size = new Size(34, 22),
				TextAlign = ContentAlignment.MiddleCenter
			};
			pnlCard.Controls.Add(lblIcon);

			Label lblName = new Label
			{
				Text = dev.Name,
				Font = UiTheme.DisplayFont(9.8f, FontStyle.Bold),
				ForeColor = UiTheme.TextPrimary,
				BackColor = UiTheme.BgCard,
				Location = new Point(56, 12),
				Size = new Size(340, 20),
				AutoEllipsis = true
			};
			pnlCard.Controls.Add(lblName);

			string subText = string.IsNullOrEmpty(dev.Adapter) ? dev.Id : (dev.Adapter + "  •  " + dev.Id);
			Label lblSub = new Label
			{
				Text = subText,
				Font = UiTheme.MonoFont(7.4f),
				ForeColor = UiTheme.TextMuted,
				BackColor = UiTheme.BgCard,
				Location = new Point(56, 35),
				Size = new Size(340, 18),
				AutoEllipsis = true
			};
			pnlCard.Controls.Add(lblSub);

			ModernStatusPill pillStatus = new ModernStatusPill(dev.Installed ? "ACTIVE" : "IDLE")
			{
				Size = new Size(88, 24),
				Anchor = (AnchorStyles.Top | AnchorStyles.Right),
				Location = new Point(pnlCard.Width - 208, 22),
				DotColor = dev.Installed ? UiTheme.AccentGreen : UiTheme.TextMuted,
				PillTextColor = dev.Installed ? UiTheme.TextBody : UiTheme.TextMuted
			};
			pnlCard.Controls.Add(pillStatus);

			ModernActionButton btnToggle = new ModernActionButton
			{
				Text = dev.Installed ? "Turn OFF" : "Turn ON",
				Size = new Size(92, 30),
				Anchor = (AnchorStyles.Top | AnchorStyles.Right),
				Location = new Point(pnlCard.Width - 110, 19),
				CornerRadius = 6,
				Font = UiTheme.DisplayFont(8.4f, FontStyle.Bold)
			};

			if (dev.Installed)
			{
				btnToggle.NormalBackColor = UiTheme.BgWell;
				btnToggle.HoverBackColor = UiTheme.BgChip;
				btnToggle.NormalBorderColor = UiTheme.BorderRing;
				btnToggle.HoverBorderColor = UiTheme.AccentAlert;
				btnToggle.TextColor = UiTheme.TextMuted;
				btnToggle.HoverTextColor = UiTheme.TextPrimary;
			}
			else
			{
				btnToggle.NormalBackColor = UiTheme.BgWell;
				btnToggle.HoverBackColor = UiTheme.BgChip;
				btnToggle.NormalBorderColor = UiTheme.AccentGreen;
				btnToggle.HoverBorderColor = UiTheme.AccentGreen;
				btnToggle.TextColor = UiTheme.AccentGreen;
				btnToggle.HoverTextColor = UiTheme.TextPrimary;
			}

			btnToggle.Click += delegate
			{
				btnToggle.Enabled = false;
				btnToggle.Text = "...";
				bool targetEnable = !dev.Installed;

				ThreadPool.QueueUserWorkItem(delegate
				{
					bool ok = ToggleDeviceApoDirect(dev.Id, dev.RenderKey, targetEnable);
					if (!IsDisposed && IsHandleCreated)
					{
						BeginInvoke((MethodInvoker)delegate
						{
							RefreshDeviceList();
						});
					}
				});
			};
			pnlCard.Controls.Add(btnToggle);

			return pnlCard;
		}

		private static bool ToggleDeviceApoDirect(string deviceId, string renderKey, bool enable)
		{
			try
			{
				string baseDir = AppDomain.CurrentDomain.BaseDirectory;
				string scriptPath = Path.Combine(baseDir, "engine", "device-manager.ps1");
				if (!File.Exists(scriptPath))
				{
					scriptPath = @"C:\Program Files\DolphinXVDO\engine\device-manager.ps1";
				}
				if (!File.Exists(scriptPath))
				{
					return false;
				}

				string typeArg = (renderKey == "Capture") ? "Recording" : "Playback";
				string args = string.Format("-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File \"{0}\" -Action set -DeviceId \"{1}\" -Type \"{2}\" -Enable ${3}",
					scriptPath, deviceId, typeArg, enable ? "true" : "false");

				ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", args)
				{
					CreateNoWindow = true,
					UseShellExecute = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					Verb = "runas"
				};
				Process p = Process.Start(psi);
				if (p != null)
				{
					p.WaitForExit(10000);
					return p.ExitCode == 0;
				}
			}
			catch { }
			return false;
		}

		public static void RestartAudioEngine()
		{
			try
			{
				string baseDir = AppDomain.CurrentDomain.BaseDirectory;
				string scriptPath = Path.Combine(baseDir, "engine", "device-manager.ps1");
				if (!File.Exists(scriptPath))
				{
					scriptPath = @"C:\Program Files\DolphinXVDO\engine\device-manager.ps1";
				}
				if (File.Exists(scriptPath))
				{
					ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File \"" + scriptPath + "\" -Action restart-audio")
					{
						CreateNoWindow = true,
						UseShellExecute = true,
						WindowStyle = ProcessWindowStyle.Hidden,
						Verb = "runas"
					};
					Process p = Process.Start(psi);
					if (p != null) p.WaitForExit(8000);
				}
			}
			catch { }
		}
	}

	public class VstRackDialog : Form
	{
		private MainForm main;
		private Panel pnlList;
		private List<CustomVstItem> tempPlugins;
		private Dictionary<string, VstEditorWindow> activeEditors = new Dictionary<string, VstEditorWindow>(StringComparer.OrdinalIgnoreCase);

		public VstRackDialog(MainForm parent)
		{
			main = parent;
			tempPlugins = new List<CustomVstItem>();
			if (main.CustomVstPlugins != null)
			{
				foreach (CustomVstItem item in main.CustomVstPlugins)
				{
					tempPlugins.Add(new CustomVstItem(item.Name, item.FilePath, item.Enabled, item.ChunkData));
				}
			}

			try
			{
				if (main != null && main.Icon != null)
				{
					base.Icon = main.Icon;
				}
			}
			catch
			{
			}

			base.Text = "Custom VST Plugin Rack - DolphinXvdo v2.3";
			base.Size = new Size(680, 520);
			base.StartPosition = FormStartPosition.CenterParent;
			BackColor = UiTheme.BgCanvas;
			ForeColor = UiTheme.TextBody;
			Font = UiTheme.DisplayFont(9f);
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = true;

			InitializeUI();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			MainForm.ApplyDiscordDarkStyle(base.Handle);
		}

		private void AutoSyncAndSave()
		{
			if (main != null)
			{
				main.CustomVstPlugins = tempPlugins;
				main.SaveDedicatedState();
				main.SyncAudioConfigOnly();
				main.UpdateVstRackButtonText();
			}
		}

		private void InitializeUI()
		{
			Panel pnlHeader = new Panel
			{
				Dock = DockStyle.Top,
				Height = 62,
				BackColor = UiTheme.BgCard,
				Padding = new Padding(16, 10, 16, 8)
			};
			pnlHeader.Paint += delegate(object s, PaintEventArgs pe)
			{
				using (Pen pen = new Pen(UiTheme.BorderSubtle, 1f))
				{
					pe.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
				}
			};
			Label lblTitle = new Label
			{
				Text = "Custom VST Plugin Rack",
				Font = UiTheme.DisplayFont(11.5f, FontStyle.Bold),
				ForeColor = UiTheme.TextPrimary,
				Location = new Point(14, 8),
				AutoSize = true
			};
			Label lblSub = new Label
			{
				Text = "Insert external 64-bit VST 2.4 plugins (.dll) into the Equalizer APO audio chain",
				ForeColor = UiTheme.TextMuted,
				Location = new Point(15, 33),
				AutoSize = true,
				Font = UiTheme.DisplayFont(8.4f)
			};
			pnlHeader.Controls.Add(lblTitle);
			pnlHeader.Controls.Add(lblSub);

			ModernActionButton btnAdd = new ModernActionButton
			{
				Text = "+ Add VST Plugin (.dll)",
				CornerRadius = 6,
				NormalBackColor = UiTheme.CtaBg,
				HoverBackColor = UiTheme.CtaHoverBg,
				PressedBackColor = UiTheme.CtaPressedBg,
				NormalBorderColor = UiTheme.CtaBg,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.CtaText,
				HoverTextColor = UiTheme.CtaText,
				Size = new Size(164, 32),
				Anchor = (AnchorStyles.Top | AnchorStyles.Right),
				Location = new Point(pnlHeader.Width - 180, 15),
				Font = UiTheme.DisplayFont(8.4f, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			btnAdd.Click += delegate
			{
				PromptAddVst();
			};
			pnlHeader.Controls.Add(btnAdd);
			base.Controls.Add(pnlHeader);

			Panel pnlBottom = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 54,
				BackColor = UiTheme.BgCard,
				Padding = new Padding(16, 10, 16, 10)
			};
			pnlBottom.Paint += delegate(object s, PaintEventArgs pe)
			{
				using (Pen pen = new Pen(UiTheme.BorderSubtle, 1f))
				{
					pe.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
				}
			};
			Label lblBottomTip = new Label
			{
				Text = "● Real-time Auto-Sync ลง APO ทันที (ไม่ต้องกดบันทึก)",
				ForeColor = UiTheme.AccentGreen,
				AutoSize = true,
				Location = new Point(14, 17),
				Font = UiTheme.DisplayFont(8.8f, FontStyle.Bold)
			};
			pnlBottom.Controls.Add(lblBottomTip);

			ModernActionButton btnClose = new ModernActionButton
			{
				Text = "ปิดหน้าต่าง (Close)",
				CornerRadius = 6,
				NormalBackColor = UiTheme.BgWell,
				HoverBackColor = UiTheme.BgChip,
				NormalBorderColor = UiTheme.BorderRing,
				HoverBorderColor = UiTheme.AccentBlue,
				TextColor = UiTheme.TextBody,
				HoverTextColor = UiTheme.TextPrimary,
				Size = new Size(146, 32),
				Anchor = (AnchorStyles.Top | AnchorStyles.Right),
				Location = new Point(pnlBottom.Width - 162, 11),
				Font = UiTheme.DisplayFont(8.8f, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			btnClose.Click += delegate
			{
				Close();
			};
			pnlBottom.Controls.Add(btnClose);
			base.Controls.Add(pnlBottom);

			pnlList = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				Padding = new Padding(14, 10, 14, 10),
				BackColor = UiTheme.BgCanvas
			};
			base.Controls.Add(pnlList);
			pnlList.BringToFront();

			RefreshListUI();
		}

		private void RefreshListUI()
		{
			pnlList.SuspendLayout();
			pnlList.Controls.Clear();

			if (tempPlugins == null || tempPlugins.Count == 0)
			{
				Label lblEmpty = new Label
				{
					Text = "No custom VST plugins added yet.\r\n\r\nClick \"+ Add VST Plugin (.dll)\" to insert 64-bit VST effects.",
					ForeColor = UiTheme.TextMuted,
					Font = UiTheme.DisplayFont(9.5f),
					TextAlign = ContentAlignment.MiddleCenter,
					Dock = DockStyle.Fill
				};
				pnlList.Controls.Add(lblEmpty);
			}
			else
			{
				int y = 8;
				for (int i = 0; i < tempPlugins.Count; i++)
				{
					int index = i;
					CustomVstItem item = tempPlugins[i];

					DoubleBufferedPanel pnlCard = new DoubleBufferedPanel
					{
						Location = new Point(8, y),
						Size = new Size(pnlList.ClientSize.Width - 28, 56),
						Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
						BackColor = UiTheme.BgCanvas
					};
					pnlCard.Paint += delegate(object s, PaintEventArgs e)
					{
						Color bColor = (item.Enabled ? UiTheme.BorderRing : UiTheme.BorderSubtle);
						UiTheme.DrawRoundedSurface(e.Graphics, pnlCard.Width, pnlCard.Height, 7f, UiTheme.BgCard, bColor, 1f);
					};

					Label lblNum = new Label
					{
						Text = string.Format("#{0}", index + 1),
						ForeColor = (item.Enabled ? UiTheme.AccentBlue : UiTheme.TextDim),
						BackColor = UiTheme.BgCard,
						Font = UiTheme.MonoFont(9.5f, FontStyle.Bold),
						Location = new Point(12, 18),
						AutoSize = true
					};
					pnlCard.Controls.Add(lblNum);

					Label lblName = new Label
					{
						Text = item.Name,
						ForeColor = (item.Enabled ? UiTheme.TextPrimary : UiTheme.TextMuted),
						BackColor = UiTheme.BgCard,
						Font = UiTheme.DisplayFont(9.5f, FontStyle.Bold),
						Location = new Point(44, 9),
						AutoSize = true
					};
					pnlCard.Controls.Add(lblName);

					bool hasState = !string.IsNullOrEmpty(item.ChunkData);
					Label lblPath = new Label
					{
						Text = item.FilePath + (hasState ? "  •  [State Saved]" : ""),
						ForeColor = (hasState ? UiTheme.AccentGreen : UiTheme.TextMuted),
						BackColor = UiTheme.BgCard,
						Font = UiTheme.MonoFont(7.4f),
						Location = new Point(45, 31),
						Size = new Size(240, 16),
						AutoEllipsis = true
					};
					pnlCard.Controls.Add(lblPath);

					int rightX = pnlCard.Width - 10;

					ModernActionButton btnDelete = new ModernActionButton
					{
						Text = "✕",
						Size = new Size(28, 28),
						Location = new Point(rightX - 30, 14),
						Anchor = (AnchorStyles.Top | AnchorStyles.Right),
						CornerRadius = 6,
						NormalBackColor = UiTheme.BgWell,
						HoverBackColor = UiTheme.BgChip,
						NormalBorderColor = UiTheme.BorderRing,
						HoverBorderColor = UiTheme.AccentAlert,
						TextColor = UiTheme.TextMuted,
						HoverTextColor = UiTheme.TextPrimary,
						Font = UiTheme.DisplayFont(8.5f, FontStyle.Bold)
					};
					btnDelete.Click += delegate
					{
						tempPlugins.RemoveAt(index);
						AutoSyncAndSave();
						RefreshListUI();
					};
					pnlCard.Controls.Add(btnDelete);

					ModernActionButton btnDown = new ModernActionButton
					{
						Text = "▼",
						Size = new Size(28, 28),
						Location = new Point(rightX - 62, 14),
						Anchor = (AnchorStyles.Top | AnchorStyles.Right),
						CornerRadius = 6,
						NormalBackColor = UiTheme.BgWell,
						HoverBackColor = UiTheme.BgChip,
						NormalBorderColor = UiTheme.BorderRing,
						HoverBorderColor = UiTheme.AccentBlue,
						TextColor = UiTheme.TextBody,
						HoverTextColor = UiTheme.TextPrimary,
						Font = UiTheme.DisplayFont(7.5f),
						Enabled = (index < tempPlugins.Count - 1)
					};
					btnDown.Click += delegate
					{
						if (index < tempPlugins.Count - 1)
						{
							CustomVstItem value = tempPlugins[index];
							tempPlugins[index] = tempPlugins[index + 1];
							tempPlugins[index + 1] = value;
							AutoSyncAndSave();
							RefreshListUI();
						}
					};
					pnlCard.Controls.Add(btnDown);

					ModernActionButton btnUp = new ModernActionButton
					{
						Text = "▲",
						Size = new Size(28, 28),
						Location = new Point(rightX - 94, 14),
						Anchor = (AnchorStyles.Top | AnchorStyles.Right),
						CornerRadius = 6,
						NormalBackColor = UiTheme.BgWell,
						HoverBackColor = UiTheme.BgChip,
						NormalBorderColor = UiTheme.BorderRing,
						HoverBorderColor = UiTheme.AccentBlue,
						TextColor = UiTheme.TextBody,
						HoverTextColor = UiTheme.TextPrimary,
						Font = UiTheme.DisplayFont(7.5f),
						Enabled = (index > 0)
					};
					btnUp.Click += delegate
					{
						if (index > 0)
						{
							CustomVstItem value = tempPlugins[index];
							tempPlugins[index] = tempPlugins[index - 1];
							tempPlugins[index - 1] = value;
							AutoSyncAndSave();
							RefreshListUI();
						}
					};
					pnlCard.Controls.Add(btnUp);

					ModernActionButton btnEdit = new ModernActionButton
					{
						Text = "Edit GUI",
						Size = new Size(80, 28),
						Location = new Point(rightX - 180, 14),
						Anchor = (AnchorStyles.Top | AnchorStyles.Right),
						CornerRadius = 6,
						NormalBackColor = UiTheme.BgWell,
						HoverBackColor = UiTheme.BgChip,
						NormalBorderColor = UiTheme.BorderRing,
						HoverBorderColor = UiTheme.AccentBlue,
						TextColor = UiTheme.AccentBlue,
						HoverTextColor = UiTheme.TextPrimary,
						Font = UiTheme.DisplayFont(8f, FontStyle.Bold)
					};
					btnEdit.Click += delegate
					{
						OpenVstEditor(item);
					};
					pnlCard.Controls.Add(btnEdit);

					ModernActionButton btnToggle = new ModernActionButton
					{
						Text = (item.Enabled ? "ACTIVE" : "BYPASS"),
						Size = new Size(74, 28),
						Location = new Point(rightX - 260, 14),
						Anchor = (AnchorStyles.Top | AnchorStyles.Right),
						CornerRadius = 14,
						NormalBackColor = UiTheme.BgChip,
						HoverBackColor = UiTheme.BgPressed,
						NormalBorderColor = (item.Enabled ? UiTheme.AccentGreen : UiTheme.BorderRing),
						HoverBorderColor = (item.Enabled ? UiTheme.AccentGreen : UiTheme.AccentBlue),
						TextColor = (item.Enabled ? UiTheme.AccentGreen : UiTheme.TextMuted),
						HoverTextColor = UiTheme.TextPrimary,
						Font = UiTheme.MonoFont(7.6f, FontStyle.Bold)
					};
					btnToggle.Click += delegate
					{
						item.Enabled = !item.Enabled;
						AutoSyncAndSave();
						RefreshListUI();
					};
					pnlCard.Controls.Add(btnToggle);

					pnlList.Controls.Add(pnlCard);
					y += 64;
				}
			}
			pnlList.ResumeLayout(true);
		}

		private void OpenVstEditor(CustomVstItem item)
		{
			if (item == null || string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
			{
				MessageBox.Show("VST Plugin file not found: " + (item != null ? item.FilePath : ""), "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			try
			{
				if (activeEditors.ContainsKey(item.FilePath) && activeEditors[item.FilePath] != null && !activeEditors[item.FilePath].IsDisposed)
				{
					if (activeEditors[item.FilePath].WindowState == FormWindowState.Minimized)
					{
						activeEditors[item.FilePath].WindowState = FormWindowState.Normal;
					}
					activeEditors[item.FilePath].BringToFront();
					activeEditors[item.FilePath].Activate();
					return;
				}

				VstEditorWindow editor = new VstEditorWindow(item);
				activeEditors[item.FilePath] = editor;
				editor.FormClosed += delegate
				{
					activeEditors.Remove(item.FilePath);
					AutoSyncAndSave();
					RefreshListUI();
				};
				editor.Show(this);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error opening VST Editor:\n" + ex.Message, "VST Host Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			List<VstEditorWindow> editors = new List<VstEditorWindow>(activeEditors.Values);
			foreach (VstEditorWindow ed in editors)
			{
				if (ed != null && !ed.IsDisposed)
				{
					try { ed.Close(); } catch { }
				}
			}
			activeEditors.Clear();
			AutoSyncAndSave();
			base.OnFormClosing(e);
		}

		private void PromptAddVst()
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				openFileDialog.Title = "Select 64-bit VST 2 Plugin DLL";
				openFileDialog.Filter = "VST 2 Plugin DLL (*.dll)|*.dll|All Files (*.*)|*.*";
				if (openFileDialog.ShowDialog(this) == DialogResult.OK)
				{
					string fileName = openFileDialog.FileName;
					if (File.Exists(fileName))
					{
						if (!Is64BitDll(fileName))
						{
							MessageBox.Show("The selected DLL is NOT a 64-bit plugin.\n\nEqualizer APO and Windows 64-bit audio engine require 64-bit VST 2 plugins.\nPlease select a 64-bit DLL.", "32-Bit VST Not Supported", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
							return;
						}
						string finalPath = fileName;
						try
						{
							string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
							if (!Directory.Exists(text))
							{
								Directory.CreateDirectory(text);
							}
							string text2 = Path.Combine(text, Path.GetFileName(fileName));
							if (!fileName.Equals(text2, StringComparison.OrdinalIgnoreCase))
							{
								File.Copy(fileName, text2, true);
								finalPath = text2;
							}
						}
						catch
						{
						}
						string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
						CustomVstItem newItem = new CustomVstItem(fileNameWithoutExtension, finalPath, true);
						tempPlugins.Add(newItem);
						AutoSyncAndSave();
						RefreshListUI();

						OpenVstEditor(newItem);
					}
				}
			}
		}

		private bool Is64BitDll(string path)
		{
			try
			{
				using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (BinaryReader binaryReader = new BinaryReader(input))
					{
						if (input.Length < 64)
						{
							return false;
						}
						input.Seek(60L, SeekOrigin.Begin);
						int num = binaryReader.ReadInt32();
						if (input.Length < (long)(num + 6))
						{
							return false;
						}
						input.Seek((long)num, SeekOrigin.Begin);
						if (binaryReader.ReadUInt32() != 17744)
						{
							return false;
						}
						return binaryReader.ReadUInt16() == 34404;
					}
				}
			}
			catch
			{
				return true;
			}
		}
	}

	public class VstEditorWindow : Form
	{
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool FreeLibrary(IntPtr hModule);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern bool SetDllDirectory(string lpPathName);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

		[DllImport("user32.dll")]
		private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate IntPtr VstHostCallback(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate IntPtr VstDispatcherFunc(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate IntPtr VstPluginMainFunc(VstHostCallback hostCallback);

		private CustomVstItem vstItem;
		private IntPtr hModule = IntPtr.Zero;
		private IntPtr pEffect = IntPtr.Zero;
		private VstDispatcherFunc dispatcher;
		private VstHostCallback hostCallback;
		private System.Windows.Forms.Timer idleTimer;
		private Panel pnlHost;
		private Panel pnlFooter;
		private bool isGuiOpened = false;

		public VstEditorWindow(CustomVstItem item)
		{
			vstItem = item;
			base.Text = "VST 2.4 Editor - " + vstItem.Name;
			base.StartPosition = FormStartPosition.CenterParent;
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = true;
			BackColor = Color.FromArgb(20, 24, 30);
			ForeColor = Color.White;
			Font = new Font("Segoe UI", 9f);

			pnlFooter = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 40,
				BackColor = Color.FromArgb(24, 28, 36),
				Padding = new Padding(14, 6, 14, 6)
			};

			Label lblStatus = new Label
			{
				Text = "● บันทึกค่าลง APO อัตโนมัติ (Auto-Sync)",
				ForeColor = Color.FromArgb(70, 210, 170),
				Font = new Font("Segoe UI", 9f, FontStyle.Bold),
				Location = new Point(14, 11),
				AutoSize = true
			};
			pnlFooter.Controls.Add(lblStatus);

			Button btnClose = new Button
			{
				Text = "✓ ปิดหน้าต่าง (Done)",
				Size = new Size(136, 28),
				Anchor = (AnchorStyles.Top | AnchorStyles.Right),
				Location = new Point(pnlFooter.Width - 148, 6),
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(0, 150, 136),
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 9f, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			btnClose.FlatAppearance.BorderSize = 0;
			btnClose.Click += delegate
			{
				this.Close();
			};
			pnlFooter.Controls.Add(btnClose);

			pnlHost = new Panel
			{
				Location = new Point(0, 0),
				Size = new Size(640, 440),
				BackColor = Color.FromArgb(16, 20, 26)
			};

			base.Controls.Add(pnlHost);
			base.Controls.Add(pnlFooter);

			base.ClientSize = new Size(640, 480);
		}

		private void UpdateHostSize(int w, int h)
		{
			if (w < 50 || h < 50) return;
			int totalW = Math.Max(560, w);
			int totalH = h + 40;

			this.ClientSize = new Size(totalW, totalH);
			pnlHost.Location = new Point(0, 0);
			pnlHost.Size = new Size(totalW, h);

			try
			{
				IntPtr hChild = FindWindowEx(pnlHost.Handle, IntPtr.Zero, null, null);
				if (hChild != IntPtr.Zero)
				{
					MoveWindow(hChild, 0, 0, totalW, h, true);
				}
			}
			catch
			{
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			MainForm.ApplyDiscordDarkStyle(base.Handle);
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			InitPluginAndOpenGui();
		}

		private IntPtr HostCallbackHandler(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt)
		{
			try
			{
				switch (opcode)
				{
					case 1:
						return new IntPtr(2400);
					case 15:
						if (index > 50 && value.ToInt64() > 50)
						{
							int newW = index;
							int newH = (int)value.ToInt64();
							if (this.InvokeRequired)
							{
								this.BeginInvoke((MethodInvoker)delegate
								{
									UpdateHostSize(newW, newH);
								});
							}
							else
							{
								UpdateHostSize(newW, newH);
							}
							return new IntPtr(1);
						}
						break;
					case 16:
						return new IntPtr(48000);
					case 17:
						return new IntPtr(512);
					case 23:
						return new IntPtr(1);
					case 32:
						if (ptr != IntPtr.Zero)
						{
							byte[] bytes = System.Text.Encoding.ASCII.GetBytes("DolphinXvdo\0");
							Marshal.Copy(bytes, 0, ptr, bytes.Length);
							return new IntPtr(1);
						}
						break;
					case 33:
						if (ptr != IntPtr.Zero)
						{
							byte[] bytes = System.Text.Encoding.ASCII.GetBytes("DolphinXvdo Audio Engine\0");
							Marshal.Copy(bytes, 0, ptr, bytes.Length);
							return new IntPtr(1);
						}
						break;
					case 34:
						return new IntPtr(1780);
				}
			}
			catch
			{
			}
			return IntPtr.Zero;
		}

		private void InitPluginAndOpenGui()
		{
			try
			{
				if (string.IsNullOrEmpty(vstItem.FilePath) || !File.Exists(vstItem.FilePath))
				{
					MessageBox.Show(this, "ไม่พบไฟล์ VST DLL: " + vstItem.FilePath, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
					this.Close();
					return;
				}

				string dllDir = Path.GetDirectoryName(vstItem.FilePath);
				if (!string.IsNullOrEmpty(dllDir) && Directory.Exists(dllDir))
				{
					SetDllDirectory(dllDir);
				}

				hModule = LoadLibrary(vstItem.FilePath);
				SetDllDirectory(null);

				if (hModule == IntPtr.Zero)
				{
					int err = Marshal.GetLastWin32Error();
					MessageBox.Show(this, string.Format("ไม่สามารถโหลด VST DLL ได้ (Error Code: {0})\nไฟล์อาจต้องการ C++ Runtime หรือไม่ใช่ 64-bit DLL", err), "LoadLibrary Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
					this.Close();
					return;
				}

				IntPtr pMain = GetProcAddress(hModule, "VSTPluginMain");
				if (pMain == IntPtr.Zero)
				{
					pMain = GetProcAddress(hModule, "main");
				}

				if (pMain == IntPtr.Zero)
				{
					MessageBox.Show(this, "DLL นี้ไม่ใช่ VST 2 Plugin ที่สมบูรณ์ (ไม่พบ VSTPluginMain)", "Invalid VST Plugin", MessageBoxButtons.OK, MessageBoxIcon.Error);
					this.Close();
					return;
				}

				VstPluginMainFunc mainFunc = (VstPluginMainFunc)Marshal.GetDelegateForFunctionPointer(pMain, typeof(VstPluginMainFunc));
				hostCallback = new VstHostCallback(HostCallbackHandler);
				pEffect = mainFunc(hostCallback);

				if (pEffect == IntPtr.Zero)
				{
					MessageBox.Show(this, "การเริ่มต้น VST Plugin ล้มเหลว (AEffect pointer is NULL)", "VST Init Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					this.Close();
					return;
				}

				int magic = Marshal.ReadInt32(pEffect, 0);
				if (magic != 1450406992)
				{
					MessageBox.Show(this, "Invalid VST Plugin Header (Magic mismatch)", "VST Init Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					this.Close();
					return;
				}

				IntPtr pDispatcher = Marshal.ReadIntPtr(pEffect, 8);
				if (pDispatcher == IntPtr.Zero)
				{
					MessageBox.Show(this, "VST Dispatcher function pointer is NULL", "VST Init Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					this.Close();
					return;
				}

				dispatcher = (VstDispatcherFunc)Marshal.GetDelegateForFunctionPointer(pDispatcher, typeof(VstDispatcherFunc));

				dispatcher(pEffect, 0, 0, IntPtr.Zero, IntPtr.Zero, 0f);
				dispatcher(pEffect, 10, 0, IntPtr.Zero, IntPtr.Zero, 48000f);
				dispatcher(pEffect, 11, 0, new IntPtr(512), IntPtr.Zero, 0f);

				if (!string.IsNullOrEmpty(vstItem.ChunkData))
				{
					try
					{
						byte[] chunk = Convert.FromBase64String(vstItem.ChunkData);
						if (chunk.Length > 0)
						{
							IntPtr pBuf = Marshal.AllocHGlobal(chunk.Length);
							Marshal.Copy(chunk, 0, pBuf, chunk.Length);
							dispatcher(pEffect, 24, 1, new IntPtr(chunk.Length), pBuf, 0f);
							Marshal.FreeHGlobal(pBuf);
						}
					}
					catch
					{
					}
				}

				dispatcher(pEffect, 12, 0, new IntPtr(1), IntPtr.Zero, 0f);

				int edW = 620;
				int edH = 440;
				IntPtr pRectPtr = Marshal.AllocHGlobal(IntPtr.Size);
				try
				{
					Marshal.WriteIntPtr(pRectPtr, IntPtr.Zero);
					dispatcher(pEffect, 13, 0, IntPtr.Zero, pRectPtr, 0f);
					IntPtr pRect = Marshal.ReadIntPtr(pRectPtr);

					if (pRect != IntPtr.Zero)
					{
						short top = Marshal.ReadInt16(pRect, 0);
						short left = Marshal.ReadInt16(pRect, 2);
						short bottom = Marshal.ReadInt16(pRect, 4);
						short right = Marshal.ReadInt16(pRect, 6);
						int w = right - left;
						int h = bottom - top;
						if (w > 50 && h > 50)
						{
							edW = w;
							edH = h;
						}
					}
				}
				catch
				{
				}

				UpdateHostSize(edW, edH);

				dispatcher(pEffect, 14, 0, IntPtr.Zero, pnlHost.Handle, 0f);
				isGuiOpened = true;

				// Query rect AGAIN after open (matches Equalizer APO VSTPluginInstance logic)
				try
				{
					Marshal.WriteIntPtr(pRectPtr, IntPtr.Zero);
					dispatcher(pEffect, 13, 0, IntPtr.Zero, pRectPtr, 0f);
					IntPtr pRect = Marshal.ReadIntPtr(pRectPtr);

					if (pRect != IntPtr.Zero)
					{
						short top = Marshal.ReadInt16(pRect, 0);
						short left = Marshal.ReadInt16(pRect, 2);
						short bottom = Marshal.ReadInt16(pRect, 4);
						short right = Marshal.ReadInt16(pRect, 6);
						int w = right - left;
						int h = bottom - top;
						if (w > 50 && h > 50)
						{
							edW = w;
							edH = h;
						}
					}
				}
				catch
				{
				}
				finally
				{
					Marshal.FreeHGlobal(pRectPtr);
				}

				// Check actual rendered child window dimensions from Windows user32
				try
				{
					IntPtr hChild = FindWindowEx(pnlHost.Handle, IntPtr.Zero, null, null);
					if (hChild != IntPtr.Zero)
					{
						RECT cr;
						if (GetWindowRect(hChild, out cr))
						{
							int cw = cr.Right - cr.Left;
							int ch = cr.Bottom - cr.Top;
							if (cw > edW) edW = cw;
							if (ch > edH) edH = ch;
						}
					}
				}
				catch
				{
				}

				// Final size adjustment ensuring zero clipping
				UpdateHostSize(edW, edH);

				idleTimer = new System.Windows.Forms.Timer();
				idleTimer.Interval = 30;
				idleTimer.Tick += delegate
				{
					if (pEffect != IntPtr.Zero && dispatcher != null && isGuiOpened)
					{
						try
						{
							dispatcher(pEffect, 19, 0, IntPtr.Zero, IntPtr.Zero, 0f);
						}
						catch
						{
						}
					}
				};
				idleTimer.Start();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "ข้อผิดพลาดในการเปิด VST GUI:\n" + ex.Message, "VST Host Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				this.Close();
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (idleTimer != null)
			{
				idleTimer.Stop();
				idleTimer.Dispose();
				idleTimer = null;
			}

			if (pEffect != IntPtr.Zero && dispatcher != null)
			{
				try
				{
					IntPtr pChunkPtr = Marshal.AllocHGlobal(IntPtr.Size);
					Marshal.WriteIntPtr(pChunkPtr, IntPtr.Zero);
					IntPtr lenPtr = dispatcher(pEffect, 23, 1, IntPtr.Zero, pChunkPtr, 0f);
					long len = lenPtr.ToInt64();
					IntPtr pChunk = Marshal.ReadIntPtr(pChunkPtr);
					Marshal.FreeHGlobal(pChunkPtr);

					if (len > 0 && pChunk != IntPtr.Zero && len < 50000000)
					{
						byte[] data = new byte[(int)len];
						Marshal.Copy(pChunk, data, 0, (int)len);
						vstItem.ChunkData = Convert.ToBase64String(data);
					}
				}
				catch
				{
				}

				try
				{
					if (isGuiOpened)
					{
						dispatcher(pEffect, 15, 0, IntPtr.Zero, IntPtr.Zero, 0f);
						isGuiOpened = false;
					}
					dispatcher(pEffect, 12, 0, IntPtr.Zero, IntPtr.Zero, 0f);
					dispatcher(pEffect, 1, 0, IntPtr.Zero, IntPtr.Zero, 0f);
				}
				catch
				{
				}
				pEffect = IntPtr.Zero;
				dispatcher = null;
			}

			if (hModule != IntPtr.Zero)
			{
				try
				{
					FreeLibrary(hModule);
				}
				catch
				{
				}
				hModule = IntPtr.Zero;
			}

			base.OnFormClosing(e);
		}
	}
}
