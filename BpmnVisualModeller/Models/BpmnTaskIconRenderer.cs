using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace BpmnVisualModeller.Models
{
    /// <summary>
    /// Маркеры BPMN 2.0 в углу задачи (как в bpmn.io).
    /// </summary>
    public static class BpmnTaskIconRenderer
    {
        private const int Margin = 4;
        private const int IconSize = 14;

        public static void Draw(Graphics g, Rectangle taskRect, Task task)
        {
            if (task == null)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (task.TaskKind == BpmnTaskKind.SubProcess)
            {
                if (!task.IsExpandedSubProcess)
                    DrawCollapsedSubProcessMarker(g, taskRect);
                return;
            }

            if (task.TaskKind != BpmnTaskKind.Generic && task.TaskKind != BpmnTaskKind.CallActivity)
            {
                var iconRect = new Rectangle(
                    taskRect.X + Margin,
                    taskRect.Y + Margin,
                    IconSize,
                    IconSize);
                DrawKindIcon(g, iconRect, task.TaskKind);
            }
        }

        public static int GetTaskBorderWidth(BpmnTaskKind kind, int defaultWidth = 2)
        {
            return kind == BpmnTaskKind.CallActivity ? 3 : defaultWidth;
        }

        private static void DrawKindIcon(Graphics g, Rectangle r, BpmnTaskKind kind)
        {
            using (var pen = new Pen(Color.Black, 1.2f))
            using (var fill = new SolidBrush(Color.Black))
            {
                switch (kind)
                {
                    case BpmnTaskKind.User:
                        DrawUserIcon(g, r, pen, fill);
                        break;
                    case BpmnTaskKind.Service:
                        DrawServiceIcon(g, r, pen);
                        break;
                    case BpmnTaskKind.Send:
                        DrawEnvelopeIcon(g, r, pen, fill, filled: true);
                        break;
                    case BpmnTaskKind.Receive:
                        DrawEnvelopeIcon(g, r, pen, fill, filled: false);
                        break;
                    case BpmnTaskKind.Manual:
                        DrawManualIcon(g, r, pen);
                        break;
                    case BpmnTaskKind.BusinessRule:
                        DrawBusinessRuleIcon(g, r, pen, fill);
                        break;
                    case BpmnTaskKind.Script:
                        DrawScriptIcon(g, r, pen);
                        break;
                }
            }
        }

        private static void DrawUserIcon(Graphics g, Rectangle r, Pen pen, Brush fill)
        {
            int cx = r.X + r.Width / 2;
            g.FillEllipse(fill, cx - 2, r.Y + 1, 5, 5);
            g.DrawArc(pen, r.X + 2, r.Y + 6, r.Width - 4, r.Height - 7, 0, -180);
        }

        private static void DrawServiceIcon(Graphics g, Rectangle r, Pen pen)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            int outer = 5;
            int inner = 2;
            g.DrawEllipse(pen, cx - outer, cy - outer, outer * 2, outer * 2);
            g.DrawEllipse(pen, cx - inner, cy - inner, inner * 2, inner * 2);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                int x1 = cx + (int)(inner * Math.Cos(angle));
                int y1 = cy + (int)(inner * Math.Sin(angle));
                int x2 = cx + (int)(outer * Math.Cos(angle));
                int y2 = cy + (int)(outer * Math.Sin(angle));
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }

        private static void DrawEnvelopeIcon(Graphics g, Rectangle r, Pen pen, Brush fill, bool filled)
        {
            var body = new Rectangle(r.X + 1, r.Y + 4, r.Width - 2, r.Height - 5);
            if (filled)
                g.FillRectangle(fill, body);
            g.DrawRectangle(pen, body);
            g.DrawLine(pen, body.Left, body.Top, body.Left + body.Width / 2, body.Top + body.Height / 2);
            g.DrawLine(pen, body.Right, body.Top, body.Left + body.Width / 2, body.Top + body.Height / 2);
        }

        private static void DrawManualIcon(Graphics g, Rectangle r, Pen pen)
        {
            Point palm = new Point(r.X + 3, r.Y + r.Height - 3);
            g.DrawLine(pen, palm, new Point(r.Right - 2, r.Y + 4));
            g.DrawLine(pen, palm, new Point(r.X + 5, r.Y + 6));
            g.DrawLine(pen, palm, new Point(r.X + 7, r.Y + 3));
            g.DrawLine(pen, palm, new Point(r.X + 9, r.Y + 5));
        }

        private static void DrawBusinessRuleIcon(Graphics g, Rectangle r, Pen pen, Brush fill)
        {
            var grid = new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
            g.FillRectangle(fill, grid.X, grid.Y, grid.Width, 4);
            g.DrawRectangle(pen, grid);
            int rowH = (grid.Height - 4) / 3;
            for (int row = 1; row <= 3; row++)
            {
                int y = grid.Y + 4 + row * rowH;
                g.DrawLine(pen, grid.X, y, grid.Right, y);
            }
            g.DrawLine(pen, grid.X + grid.Width / 2, grid.Y + 4, grid.X + grid.Width / 2, grid.Bottom);
        }

        private static void DrawScriptIcon(Graphics g, Rectangle r, Pen pen)
        {
            var doc = new Rectangle(r.X + 3, r.Y + 1, r.Width - 5, r.Height - 2);
            g.DrawRectangle(pen, doc);
            for (int i = 0; i < 3; i++)
            {
                int y = doc.Y + 4 + i * 3;
                g.DrawLine(pen, doc.X + 2, y, doc.Right - 2, y);
            }
        }

        private static void DrawCollapsedSubProcessMarker(Graphics g, Rectangle taskRect)
        {
            int box = 11;
            int x = taskRect.X + (taskRect.Width - box) / 2;
            int y = taskRect.Bottom - box - 3;

            using (var pen = new Pen(Color.Black, 1.2f))
            {
                g.DrawRectangle(pen, x, y, box, box);
                int cx = x + box / 2;
                int cy = y + box / 2;
                g.DrawLine(pen, cx, y + 2, cx, y + box - 2);
                g.DrawLine(pen, x + 2, cy, x + box - 2, cy);
            }
        }
    }
}
