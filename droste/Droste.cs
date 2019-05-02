using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System;
using System.Drawing;
using System.Numerics;
using System.Reflection;

namespace Droste
{
    public class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author => base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
        public string Copyright => base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
        public string DisplayName => base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
        public Version Version => base.GetType().Assembly.GetName().Version;
        public Uri WebsiteUri => new Uri("https://forums.getpaint.net/index.php?showtopic=32240");
    }

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Droste")]
    public class DrosteFx : PropertyBasedEffect
    {
        private const string StaticName = "Droste";
        private static readonly Image StaticIcon = new Bitmap(typeof(DrosteFx), "Resources.Droste.png");

        public DrosteFx()
            : base(StaticName, StaticIcon, SubmenuNames.Distort, new EffectOptions { Flags = EffectFlags.Configurable })
        {
        }

        private enum PropertyNames
        {
            InnerRadius,
            OuterRadius,
            Center,
            InverseAlpha,
            RepeatPerTurn,
            Angle
        }

        protected override PropertyCollection OnCreatePropertyCollection()
        {
            Property[] props = new Property[]
            {
                new DoubleProperty(PropertyNames.InnerRadius, 0.05, 0.01, 1.00),
                new DoubleProperty(PropertyNames.OuterRadius, 0.25, 0.01, 1.00),
                new DoubleVectorProperty(PropertyNames.Center, Pair.Create(0.0, 0.0), Pair.Create(-2.0, -2.0), Pair.Create(+2.0, +2.0)),
                new BooleanProperty(PropertyNames.InverseAlpha, true),
                new Int32Property(PropertyNames.RepeatPerTurn, 1, 1, 10),
                new DoubleProperty(PropertyNames.Angle, 0, -180, +180)
            };

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.InnerRadius, ControlInfoPropertyNames.DisplayName, "Inner Radius");
            configUI.SetPropertyControlValue(PropertyNames.InnerRadius, ControlInfoPropertyNames.SliderLargeChange, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.InnerRadius, ControlInfoPropertyNames.SliderSmallChange, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.InnerRadius, ControlInfoPropertyNames.UpDownIncrement, 0.01);
            configUI.SetPropertyControlValue(PropertyNames.OuterRadius, ControlInfoPropertyNames.DisplayName, "Outer Radius");
            configUI.SetPropertyControlValue(PropertyNames.OuterRadius, ControlInfoPropertyNames.SliderLargeChange, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.OuterRadius, ControlInfoPropertyNames.SliderSmallChange, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.OuterRadius, ControlInfoPropertyNames.UpDownIncrement, 0.01);
            configUI.SetPropertyControlValue(PropertyNames.Center, ControlInfoPropertyNames.DisplayName, "Center");
            configUI.SetPropertyControlValue(PropertyNames.Center, ControlInfoPropertyNames.SliderSmallChangeX, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.Center, ControlInfoPropertyNames.SliderLargeChangeX, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.Center, ControlInfoPropertyNames.UpDownIncrementX, 0.01);
            configUI.SetPropertyControlValue(PropertyNames.Center, ControlInfoPropertyNames.SliderSmallChangeY, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.Center, ControlInfoPropertyNames.SliderLargeChangeY, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.Center, ControlInfoPropertyNames.UpDownIncrementY, 0.01);
            Rectangle selRect = EnvironmentParameters.GetSelection(EnvironmentParameters.SourceSurface.Bounds).GetBoundsInt();
            ImageResource imageRes = ImageResource.FromImage(EnvironmentParameters.SourceSurface.CreateAliasedBitmap(selRect));
            configUI.SetPropertyControlValue(PropertyNames.Center, ControlInfoPropertyNames.StaticImageUnderlay, imageRes);
            configUI.SetPropertyControlValue(PropertyNames.InverseAlpha, ControlInfoPropertyNames.DisplayName, "Inverse Transparency");
            configUI.SetPropertyControlValue(PropertyNames.InverseAlpha, ControlInfoPropertyNames.Description, "Inverse Transparency");
            configUI.SetPropertyControlValue(PropertyNames.RepeatPerTurn, ControlInfoPropertyNames.DisplayName, "Repeat Per Turn");
            configUI.SetPropertyControlValue(PropertyNames.Angle, ControlInfoPropertyNames.DisplayName, "Angle Correction");
            configUI.SetPropertyControlType(PropertyNames.Angle, PropertyControlType.AngleChooser);

            return configUI;
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Bounds).GetBoundsInt();
            double rmax = Math.Max(selection.Height, selection.Width);

            innerRadius = newToken.GetProperty<DoubleProperty>(PropertyNames.InnerRadius).Value * rmax;
            outerRadius = newToken.GetProperty<DoubleProperty>(PropertyNames.OuterRadius).Value * rmax;
            center = newToken.GetProperty<DoubleVectorProperty>(PropertyNames.Center).Value;
            inverseAlpha = newToken.GetProperty<BooleanProperty>(PropertyNames.InverseAlpha).Value;
            repeatPerTurn = newToken.GetProperty<Int32Property>(PropertyNames.RepeatPerTurn).Value;
            angle = newToken.GetProperty<DoubleProperty>(PropertyNames.Angle).Value;

            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected override void OnRender(Rectangle[] renderRects, int startIndex, int length)
        {
            if (length == 0) return;
            for (int i = startIndex; i < startIndex + length; ++i)
            {
                Render(DstArgs.Surface, SrcArgs.Surface, renderRects[i]);
            }
        }

        private double innerRadius = 0.05;
        private double outerRadius = 0.25;
        private Pair<double, double> center = Pair.Create(0.0, 0.0);
        private bool inverseAlpha = true;
        private int repeatPerTurn = 1;
        private double angle = 0;

        private static ColorBgra AddColor(ColorBgra original, ColorBgra addition)
        {
            if (original.A == 255)
            {
                return original;
            }
            if (original.A == 0)
            {
                return addition;
            }
            byte addition_alpha = Math.Min(addition.A, (byte)(255 - original.A));
            int total_alpha = original.A + addition_alpha;
            double orig_frac = original.A / (double)total_alpha;
            double add_frac = addition_alpha / (double)total_alpha;
            return ColorBgra.FromBgraClamped(
                (int)(original.B * orig_frac + addition.B * add_frac),
                (int)(original.G * orig_frac + addition.G * add_frac),
                (int)(original.R * orig_frac + addition.R * add_frac),
                total_alpha);
        }

        private void Render(Surface dst, Surface src, Rectangle rect)
        {
            Rectangle selection = EnvironmentParameters.GetSelection(src.Bounds).GetBoundsInt();
            long CenterX = (long)((1.0 + center.First) * ((selection.Right - selection.Left) / 2) + selection.Left);
            long CenterY = (long)((1.0 + center.Second) * ((selection.Bottom - selection.Top) / 2) + selection.Top);

            double rfrac = Math.Log(outerRadius / innerRadius);
            double alpha = Math.Atan2(rfrac, Math.PI * 2);
            double f = Math.Cos(alpha);
            Complex ialpha = new Complex(0, alpha);
            Complex beta = f * Complex.Exp(ialpha);

            Complex zin;
            Complex ztemp1;
            Complex ztemp2;
            Complex zout;

            float from_x;
            float from_y;

            float rotatedX;
            float rotatedY;
            double angleRad = angle * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            ColorBgra result;

            for (int y = rect.Top; y < rect.Bottom; ++y)
            {
                if (IsCancelRequested) return;
                for (int x = rect.Left; x < rect.Right; ++x)
                {
                    zout = new Complex(x - CenterX, CenterY - y);
                    result = ColorBgra.Zero;

                    // see algorithm description here : http://www.josleys.com/articles/printgallery.htm

                    // inverse step 3 and inverse step 2 ==> output after step 1
                    ztemp1 = Complex.Log(zout) / beta;

                    for (int layer = 0; layer < 3 && result.A < 255; layer++)
                    {
                        // convert to a point with real part inside [0 , log (r1/r2)]
                        // combine with data in [log (r1/r2), 2 * log (r1/r2)] if not opaque
                        // maybe even go to the part after that etc...
                        double rtemp = inverseAlpha ?
                            ztemp1.Real % rfrac + (2 - layer) * rfrac :
                            ztemp1.Real % rfrac + layer * rfrac;

                        ztemp2 = new Complex(rtemp, ztemp1.Imaginary * repeatPerTurn);

                        // inverse step 1
                        zin = innerRadius * Complex.Exp(ztemp2);

                        from_x = (float)(zin.Real + CenterX);
                        from_y = (float)(CenterY - zin.Imaginary);

                        rotatedX = (float)((from_x - CenterX) * cos - (from_y - CenterY) * sin + CenterX);
                        rotatedY = (float)((from_y - CenterY) * cos - (from_x - CenterX) * -1.0 * sin + CenterY);

                        result = AddColor(result, src.GetBilinearSample(rotatedX, rotatedY));
                    }
                    dst[x, y] = result;
                }
            }
        }
    }
}
