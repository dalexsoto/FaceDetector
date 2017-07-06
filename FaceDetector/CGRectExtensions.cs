using System;
using CoreGraphics;
namespace FaceDetector {
	public static class CGRectExtensions {
		public static CGRect Scale (this CGRect self, CGSize size) {
			return new CGRect (
				x: self.X * size.Width,
				y: self.Y * size.Height,
				width: self.Width * size.Width,
				height: self.Height * size.Height
			);
		}
	}
}
