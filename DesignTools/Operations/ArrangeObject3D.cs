﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	using Aabb = AxisAlignedBoundingBox;

	public class ArrangeObject3D : Object3D, IRebuildable, IPropertyGridModifier
	{
		// We need to serialize this so we can remove the arrange and get back to the objects before arranging
		public List<Aabb> ChildrenBounds = new List<Aabb>();

		public ArrangeObject3D()
		{
			Name = "Arrange".Localize();
		}

		public enum Align { None, Min, Center, Max }

		public override string ActiveEditor => "PublicPropertyEditor";

		public bool Advanced { get; set; } = false;

		public override bool CanBake => true;

		public override bool CanRemove => true;

		[DisplayName("X")]
		[Icons(new string[] { "424.png", "align_left.png", "align_center_x.png", "align_right.png" })]
		public Align XAlign { get; set; } = Align.None;

		[DisplayName("Start X")]
		[Icons(new string[] { "424.png", "align_to_left.png", "align_to_center_x.png", "align_to_right.png" })]
		public Align XAlignTo { get; set; } = Align.None;

		[DisplayName("Offset X")]
		public double XOffset { get; set; } = 0;

		[DisplayName("Y")]
		[Icons(new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_top.png" })]
		public Align YAlign { get; set; } = Align.None;

		[DisplayName("Start Y")]
		[Icons(new string[] { "424.png", "align_to_bottom.png", "align_to_center_y.png", "align_to_top.png" })]
		public Align YAlignTo { get; set; } = Align.None;

		[DisplayName("Offset Y")]
		public double YOffset { get; set; } = 0;

		[DisplayName("Z")]
		[Icons(new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_top.png" })]
		public Align ZAlign { get; set; } = Align.None;

		[DisplayName("Start Z")]
		[Icons(new string[] { "424.png", "align_to_bottom.png", "align_to_center_y.png", "align_to_top.png" })]
		public Align ZAlignTo { get; set; } = Align.None;

		[DisplayName("Offset Z")]
		public double ZOffset { get; set; } = 0;

		public void Rebuild()
		{
			var aabb = this.GetAxisAlignedBoundingBox();

			// TODO: check if the has code for the children
			if (ChildrenBounds.Count == 0)
			{
				this.Children.Modify(list =>
				{
					foreach (var child in list)
					{
						ChildrenBounds.Add(child.GetAxisAlignedBoundingBox());
					}
				});
			}

			this.Children.Modify(list =>
			{
				var firstBounds = ChildrenBounds[0];
				int i = 0;
				foreach (var child in list)
				{
					if (i > 0)
					{
						if (XAlign == Align.None)
						{
							// make sure it is where it started
							AlignAxis(0, Align.Min, ChildrenBounds[i].minXYZ.X, 0, child);
						}
						else
						{
							AlignAxis(0, XAlign, GetAlignToOffset(0, (!Advanced || XAlignTo == Align.None) ? XAlign : XAlignTo), XOffset, child);
						}
						if (YAlign == Align.None)
						{
							AlignAxis(1, Align.Min, ChildrenBounds[i].minXYZ.Y, 0, child);
						}
						else
						{
							AlignAxis(1, YAlign, GetAlignToOffset(1, (!Advanced || YAlignTo == Align.None) ? YAlign : YAlignTo), YOffset, child);
						}
						if (ZAlign == Align.None)
						{
							AlignAxis(2, Align.Min, ChildrenBounds[i].minXYZ.Z, 0, child);
						}
						else
						{
							AlignAxis(2, ZAlign, GetAlignToOffset(2, (!Advanced || ZAlignTo == Align.None) ? ZAlign : ZAlignTo), ZOffset, child);
						}
					}
					i++;
				}
			});
		}

		public override void Remove()
		{
			// put everything back to where it was before the arange started
			if (ChildrenBounds.Count == Children.Count)
			{
				int i = 0;
				foreach (var child in Children)
				{
					// Where you are minus where you started to get back to where you started
					child.Translate(-(child.GetAxisAlignedBoundingBox().minXYZ - ChildrenBounds[i].minXYZ));
					i++;
				}
			}

			base.Remove();
		}

		public void UpdateControls(PublicPropertyEditor editor)
		{
			editor.GetEditRow(nameof(XAlignTo)).Visible = Advanced;
			editor.GetEditRow(nameof(XOffset)).Visible = Advanced;
			editor.GetEditRow(nameof(YAlignTo)).Visible = Advanced;
			editor.GetEditRow(nameof(YOffset)).Visible = Advanced;
			editor.GetEditRow(nameof(ZAlignTo)).Visible = Advanced;
			editor.GetEditRow(nameof(ZOffset)).Visible = Advanced;
		}

		private void AlignAxis(int axis, Align align, double alignTo, double offset,
			IObject3D item)
		{
			var aabb = item.GetAxisAlignedBoundingBox();
			var translate = Vector3.Zero;

			switch (align)
			{
				case Align.Min:
					translate[axis] = alignTo - aabb.minXYZ[axis] + offset;
					break;

				case Align.Center:
					translate[axis] = alignTo - aabb.Center[axis] + offset;
					break;

				case Align.Max:
					translate[axis] = alignTo - aabb.maxXYZ[axis] + offset;
					break;
			}

			item.Translate(translate);
		}

		private double GetAlignToOffset(int axis, Align alignTo)
		{
			switch (alignTo)
			{
				case Align.Min:
					return ChildrenBounds[0].minXYZ[axis];

				case Align.Center:
					return ChildrenBounds[0].Center[axis];

				case Align.Max:
					return ChildrenBounds[0].maxXYZ[axis];

				default:
					throw new NotImplementedException();
			}
		}
	}
}