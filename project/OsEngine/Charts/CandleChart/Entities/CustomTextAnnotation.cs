using CustomAnnotations.CustomAnnotations;
using OxyPlot;
using OxyPlot.Annotations;
using System;
using System.Collections.Generic;

namespace CustomAnnotations
{
    public class CustomTextAnnotation : CustomTextualAnnotation
    {
        public IList<ScreenPoint> actualBounds;

        public CustomTextAnnotation()
        {
            this.Stroke = OxyColors.Black;
            this.Background = OxyColors.Undefined;
            this.StrokeThickness = 1;
            this.TextVerticalAlignment = VerticalAlignment.Bottom;
            this.Padding = new OxyThickness(4);
        }

        public OxyColor Background { get; set; }

        public ScreenVector Offset { get; set; }

        public OxyThickness Padding { get; set; }

        public OxyColor Stroke { get; set; }

        public double StrokeThickness { get; set; }

        /// <inheritdoc/>
        public override void Render(IRenderContext rc)
        {
            ScreenPoint position = TextPosition + this.Orientate(this.Offset);

            var textSize = rc.MeasureText(this.Text, this.ActualFont, this.ActualFontSize, this.ActualFontWeight);

            this.GetActualTextAlignment(out var ha, out var va);

            this.actualBounds = GetTextBounds(position, textSize, this.Padding, this.TextRotation, ha, va);


            OxyRect actualRect = new OxyRect(this.actualBounds[0], this.actualBounds[2]);

            rc.DrawRectangle(actualRect, this.Background, this.Stroke, this.StrokeThickness, this.EdgeRenderingMode);

            rc.DrawMathText(
                position,
                this.Text,
                this.GetSelectableFillColor(this.ActualTextColor),
                this.ActualFont,
                this.ActualFontSize,
                this.ActualFontWeight,
                this.TextRotation,
                ha,
                va);
        }

        public ScreenPoint GetDataPointPosition(DataPoint TextPosition)
        {
            return this.Transform(TextPosition) + this.Orientate(this.Offset);
        }

       
        protected override HitTestResult HitTestOverride(HitTestArguments args)
        {
            if (this.actualBounds == null)
            {
                return null;
            }

            return ScreenPointHelper.IsPointInPolygon(args.Point, this.actualBounds) ? new HitTestResult(this, args.Point) : null;
        }


        private IList<ScreenPoint> GetTextBounds(ScreenPoint position, OxySize size, OxyThickness padding, double rotation, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
        {
            double left, right, top, bottom;

            switch (horizontalAlignment)
            {
                case HorizontalAlignment.Center:
                    left = -size.Width * 0.5;
                    right = -left;
                    break;
                case HorizontalAlignment.Right:
                    left = -size.Width;
                    right = 0;
                    break;
                default:
                    left = 0;
                    right = size.Width;
                    break;
            }

            switch (verticalAlignment)
            {
                case VerticalAlignment.Middle:
                    top = -size.Height * 0.5;
                    bottom = -top;
                    break;
                case VerticalAlignment.Bottom:
                    top = -size.Height;
                    bottom = 0;
                    break;
                default:
                    top = 0;
                    bottom = size.Height;
                    break;
            }

            double cost = Math.Cos(rotation / 180 * Math.PI);
            double sint = Math.Sin(rotation / 180 * Math.PI);
            var u = new ScreenVector(cost, sint);
            var v = new ScreenVector(-sint, cost);
            var polygon = new ScreenPoint[4];
            polygon[0] = position + (u * (left - padding.Left)) + (v * (top - padding.Top));
            polygon[1] = position + (u * (right + padding.Right)) + (v * (top - padding.Top));
            polygon[2] = position + (u * (right + padding.Right)) + (v * (bottom + padding.Bottom));
            polygon[3] = position + (u * (left - padding.Left)) + (v * (bottom + padding.Bottom));
            return polygon;
        }
    }

    namespace CustomAnnotations
    {

        public abstract class CustomTextualAnnotation : TransposableAnnotation
        {

            protected CustomTextualAnnotation()
            {
                this.TextHorizontalAlignment = HorizontalAlignment.Center;
                this.TextVerticalAlignment = VerticalAlignment.Middle;
                this.TextPosition = ScreenPoint.Undefined;
                this.TextRotation = 0;
            }

            public string Text { get; set; }

            public ScreenPoint TextPosition { get; set; }

            public HorizontalAlignment TextHorizontalAlignment { get; set; }

            public VerticalAlignment TextVerticalAlignment { get; set; }

            public double TextRotation { get; set; }


            protected ScreenPoint GetActualTextPosition(Func<ScreenPoint> defaultPosition)
            {
                return TextPosition;
            }

            public override OxyRect GetClippingRect()
            {
                return OxyRect.Everything;
            }


            protected void GetActualTextAlignment(out HorizontalAlignment ha, out VerticalAlignment va)
            {
                ha = this.TextHorizontalAlignment;
                va = this.TextVerticalAlignment;
            }
        }
    }
}