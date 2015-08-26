using System;
using System.Collections.Generic;
using System.Drawing;
using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.Effects;
using PaintDotNet.PropertySystem;
using MathNet.Numerics;

namespace Droste
{
    public class DrosteFx : PropertyBasedEffect
    {
        public static string StaticName
        {
            get
            {
                return "Droste Effect Plugin";
            }
        }

        public static Bitmap StaticIcon
        {
            get
            {
                return Droste.Properties.Resources.Droste;
            }
        }

        public static string StaticSubMenuName
        {
            get
            {
                return SubmenuNames.Distort;
            }
        }

        public enum PropertyNames
        {
            InnerRadius,
            OuterRadius,
            Center,
            InverseTransparency,
            RepeatPerTurn
        }

        public DrosteFx()
            : base(StaticName, StaticIcon, StaticSubMenuName, EffectFlags.Configurable)
        {

        }

        protected override PropertyCollection OnCreatePropertyCollection()
        {
            List<Property> props = new List<Property>();
            props.Add(new DoubleProperty(PropertyNames.InnerRadius, 0.05, 0.01, 1.00));
            props.Add(new DoubleProperty(PropertyNames.OuterRadius, 0.25, 0.01, 1.00));
            props.Add(new DoubleVectorProperty(PropertyNames.Center
                                             , Pair.Create(0.0, 0.0)
                                             , Pair.Create(-2.0, -2.0)
                                             , Pair.Create(+2.0, +2.0)));
            props.Add(new BooleanProperty(PropertyNames.InverseTransparency, true));
            props.Add(new Int32Property(PropertyNames.RepeatPerTurn, 1, 1, 10));
            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);
            return configUI;
        }

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

        private double r1;
        private double r2;
        private Pair<double, double> center;
        private bool inverse_transparency;
        private int repeat_per_turn;
        
        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Bounds).GetBoundsInt();
            double rmax = Math.Max(selection.Height, selection.Width);
            this.r1 = newToken.GetProperty<DoubleProperty>(PropertyNames.InnerRadius).Value * rmax;
            this.r2 = newToken.GetProperty<DoubleProperty>(PropertyNames.OuterRadius).Value * rmax;
            this.center = newToken.GetProperty<DoubleVectorProperty>(PropertyNames.Center).Value;
            this.inverse_transparency = newToken.GetProperty<BooleanProperty>(PropertyNames.InverseTransparency).Value;
            this.repeat_per_turn = newToken.GetProperty<Int32Property>(PropertyNames.RepeatPerTurn).Value;
            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected override void  OnRender(Rectangle[] rois, int startIndex, int length)
        {  
            PdnRegion selectionRegion = EnvironmentParameters.GetSelection(SrcArgs.Bounds);
            Rectangle selection = selectionRegion.GetBoundsInt();
            long CenterX = (long)((1.0 + center.First) * ((selection.Right - selection.Left) / 2) + selection.Left);
            long CenterY = (long)((1.0 + center.Second) * ((selection.Bottom - selection.Top) / 2) + selection.Top);

            double rfrac = Math.Log(r2 / r1);
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

            ColorBgra result;

            for (int i = startIndex; i < startIndex + length; ++i)
            {
                Rectangle rect = rois[i];

                for (int y = rect.Top; y < rect.Bottom; ++y)
                {
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
                            if (inverse_transparency)
                            {
                                rtemp = ztemp1.Real % rfrac + (2 - layer) * rfrac;
                            }
                            else
                            {
                                rtemp = ztemp1.Real % rfrac + layer * rfrac;
                            }

                            ztemp2 = Complex.FromRealImaginary(rtemp, ztemp1.Imag * repeat_per_turn);

                            // inverse step 1
                            zin = r1 * ztemp2.Exponential();

                            from_x = (float)(zin.Real + CenterX);
                            from_y = (float)(CenterY - zin.Imag);

                            result = AddColor(result, SrcArgs.Surface.GetBilinearSample(from_x, from_y));
                        }
                        DstArgs.Surface[x, y] = result;
                    }
                }
            }
        }
    }
}