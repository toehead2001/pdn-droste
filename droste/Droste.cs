using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.Effects;
using PaintDotNet.PropertySystem;
using MathNet.Numerics;

namespace Droste
{
    public class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author
        {
            get
            {
                return ((AssemblyCopyrightAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;
            }
        }
        public string Copyright
        {
            get
            {
                return ((AssemblyDescriptionAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)[0]).Description;
            }
        }

        public string DisplayName
        {
            get
            {
                return ((AssemblyProductAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0]).Product;
            }
        }

        public Version Version
        {
            get
            {
                return base.GetType().Assembly.GetName().Version;
            }
        }

        public Uri WebsiteUri
        {
            get
            {
                return new Uri("http://www.getpaint.net/redirect/plugins.html");
            }
        }
    }

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Blur Fill")]

	public class DrosteFx : PropertyBasedEffect
    {
        public static string StaticName
        {
            get
            {
                return "Droste";
            }
        }

        public static Image StaticIcon
        {
            get
            {
                return Properties.Resources.Droste;
            }
        }

        public static string StaticSubMenuName
        {
            get
            {
                return SubmenuNames.Distort;
            }
        }

        public DrosteFx()
            : base(StaticName, StaticIcon, StaticSubMenuName, EffectFlags.Configurable)
        {
        }

        public enum PropertyNames
        {
            Amount1,
            Amount2,
            Amount3,
            Amount4,
            Amount5,
            Amount6
        }

        protected override PropertyCollection OnCreatePropertyCollection()
        {
            List<Property> props = new List<Property>();

            props.Add(new DoubleProperty(PropertyNames.Amount1, 0.05, 0.01, 1.00));
            props.Add(new DoubleProperty(PropertyNames.Amount2, 0.25, 0.01, 1.00));
            props.Add(new DoubleVectorProperty(PropertyNames.Amount3
                                             , Pair.Create(0.0, 0.0)
                                             , Pair.Create(-2.0, -2.0)
                                             , Pair.Create(+2.0, +2.0)));
            props.Add(new BooleanProperty(PropertyNames.Amount4, true));
            props.Add(new Int32Property(PropertyNames.Amount5, 1, 1, 10));
            props.Add(new DoubleProperty(PropertyNames.Amount6, 0, -180, +180));

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.DisplayName, "Inner Radius");
            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.SliderLargeChange, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.SliderSmallChange, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.UpDownIncrement, 0.01);
            configUI.SetPropertyControlValue(PropertyNames.Amount2, ControlInfoPropertyNames.DisplayName, "Outer Radius");
            configUI.SetPropertyControlValue(PropertyNames.Amount2, ControlInfoPropertyNames.SliderLargeChange, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.Amount2, ControlInfoPropertyNames.SliderSmallChange, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.Amount2, ControlInfoPropertyNames.UpDownIncrement, 0.01);
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.DisplayName, "Center");
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.SliderSmallChangeX, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.SliderLargeChangeX, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.UpDownIncrementX, 0.01);
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.SliderSmallChangeY, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.SliderLargeChangeY, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.UpDownIncrementY, 0.01);
            Rectangle selection3 = EnvironmentParameters.GetSelection(EnvironmentParameters.SourceSurface.Bounds).GetBoundsInt();
            ImageResource imageResource3 = ImageResource.FromImage(EnvironmentParameters.SourceSurface.CreateAliasedBitmap(selection3));
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.StaticImageUnderlay, imageResource3);
            configUI.SetPropertyControlValue(PropertyNames.Amount4, ControlInfoPropertyNames.DisplayName, "Inverse Transparency");
            configUI.SetPropertyControlValue(PropertyNames.Amount4, ControlInfoPropertyNames.Description, "Inverse Transparency");
            configUI.SetPropertyControlValue(PropertyNames.Amount5, ControlInfoPropertyNames.DisplayName, "Repeat Per Turn");
            configUI.SetPropertyControlValue(PropertyNames.Amount6, ControlInfoPropertyNames.DisplayName, "Angle Correction");
            configUI.SetPropertyControlType(PropertyNames.Amount6, PropertyControlType.AngleChooser);

            return configUI;
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Bounds).GetBoundsInt();
            double rmax = Math.Max(selection.Height, selection.Width);

            Amount1 = newToken.GetProperty<DoubleProperty>(PropertyNames.Amount1).Value * rmax;
            Amount2 = newToken.GetProperty<DoubleProperty>(PropertyNames.Amount2).Value * rmax;
            Amount3 = newToken.GetProperty<DoubleVectorProperty>(PropertyNames.Amount3).Value;
            Amount4 = newToken.GetProperty<BooleanProperty>(PropertyNames.Amount4).Value;
            Amount5 = newToken.GetProperty<Int32Property>(PropertyNames.Amount5).Value;
            Amount6 = newToken.GetProperty<DoubleProperty>(PropertyNames.Amount6).Value;

            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected override unsafe void OnRender(Rectangle[] rois, int startIndex, int length)
        {
            if (length == 0) return;
            for (int i = startIndex; i < startIndex + length; ++i)
            {
                Render(DstArgs.Surface, SrcArgs.Surface, rois[i]);
            }
        }

        double Amount1 = 0.05;
        double Amount2 = 0.25;
        Pair<double, double> Amount3 = Pair.Create(0.0, 0.0);
        bool Amount4 = true;
        int Amount5 = 1;
        double Amount6 = 0;

        public static ColorBgra AddColor(ColorBgra original, ColorBgra addition)
        {
            if (original.A == 255)
            {
                return original;
            }
            if (original.A == 0)
            {
                return addition;
            }
            byte addition_alpha = Math.Min(addition.A, (byte) (255 - original.A));
            int total_alpha = original.A + addition_alpha;
            double orig_frac = original.A / (double) total_alpha;
            double add_frac = addition_alpha / (double) total_alpha;
            return ColorBgra.FromBgra(Int32Util.ClampToByte((int)(original.B * orig_frac + addition.B * add_frac)),
                                      Int32Util.ClampToByte((int)(original.G * orig_frac + addition.G * add_frac)),
                                      Int32Util.ClampToByte((int)(original.R * orig_frac + addition.R * add_frac)),
                                      Int32Util.ClampToByte(total_alpha));
        }

        // My own version of NaturalLogarithm() that doesn't create a special case for real numbers
        public static Complex MyNaturalLogarithm(Complex input)
        {
            return Complex.FromRealImaginary(0.5d * Math.Log(input.ModulusSquared), input.Argument);
        }

        void Render(Surface dst, Surface src, Rectangle rect)
        {
            Rectangle selection = EnvironmentParameters.GetSelection(src.Bounds).GetBoundsInt();
            long CenterX = (long)((1.0 + Amount3.First) * ((selection.Right - selection.Left) / 2) + selection.Left);
            long CenterY = (long)((1.0 + Amount3.Second) * ((selection.Bottom - selection.Top) / 2) + selection.Top);

            double rfrac = Math.Log(Amount2 / Amount1);
            double alpha = Math.Atan2(rfrac, Math.PI * 2);
            double f = Math.Cos(alpha);
            Complex ialpha = Complex.FromRealImaginary(0, alpha);
            Complex beta = f * ialpha.Exponential();

            Complex zin;
            Complex ztemp1;
            Complex ztemp2;
            Complex zout;

            float from_x;
            float from_y;

            float rotatedX;
            float rotatedY;
            double angle = Amount6 * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            ColorBgra result;

            for (int y = rect.Top; y < rect.Bottom; ++y)
            {
                if (IsCancelRequested) return;
                for (int x = rect.Left; x < rect.Right; ++x)
                {
                    zout = Complex.FromRealImaginary(x - CenterX, CenterY - y);
                    result = ColorBgra.Zero;

                    // see algorithm description here : http://www.josleys.com/articles/printgallery.htm

                    // inverse step 3 and inverse step 2 ==> output after step 1
                    ztemp1 = MyNaturalLogarithm(zout) / beta;

                    for (int layer = 0; layer < 3 && result.A < 255; layer++)
                    {
                        // convert to a point with real part inside [0 , log (r1/r2)]
                        // combine with data in [log (r1/r2), 2 * log (r1/r2)] if not opaque
                        // maybe even go to the part after that etc...
                        double rtemp = 0;
                        if (Amount4)
                        {
                            rtemp = ztemp1.Real % rfrac + (2 - layer) * rfrac;
                        }
                        else
                        {
                            rtemp = ztemp1.Real % rfrac + layer * rfrac;
                        }

                        ztemp2 = Complex.FromRealImaginary(rtemp, ztemp1.Imag * Amount5);

                        // inverse step 1
                        zin = Amount1 * ztemp2.Exponential();

                        from_x = (float)(zin.Real + CenterX);
                        from_y = (float)(CenterY - zin.Imag);

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