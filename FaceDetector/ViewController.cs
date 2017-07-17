using System;

using UIKit;
using Vision;
using Foundation;
using CoreGraphics;
using AVFoundation;
using CoreAnimation;
using CoreFoundation;
using CoreMedia;
using CoreVideo;
using CoreImage;
using System.Linq;

namespace FaceDetector {
	public partial class ViewController : UIViewController, IAVCaptureVideoDataOutputSampleBufferDelegate {
		static AVCaptureSession session;
		CAShapeLayer shapeLayer = new CAShapeLayer ();

		VNDetectFaceRectanglesRequest faceDetection = new VNDetectFaceRectanglesRequest (null);
		VNDetectFaceLandmarksRequest faceLandmarks = new VNDetectFaceLandmarksRequest (null);
		VNSequenceRequestHandler faceLandmarksDetectionRequest = new VNSequenceRequestHandler ();
		VNSequenceRequestHandler faceDetectionRequest = new VNSequenceRequestHandler ();

		Lazy<AVCaptureVideoPreviewLayer> previewLayer = new Lazy<AVCaptureVideoPreviewLayer> (() => {
			if (session == null)
				return null;

			var previewLayer = new AVCaptureVideoPreviewLayer (session) {
				VideoGravity = AVLayerVideoGravity.ResizeAspectFill
			};
			return previewLayer;
		});

		AVCaptureDevice frontCamera = AVCaptureDevice.GetDefaultDevice (AVCaptureDeviceType.BuiltInWideAngleCamera, AVMediaType.Video, AVCaptureDevicePosition.Front);

		protected ViewController (IntPtr handle) : base (handle)
		{
			// Note: this .ctor should not contain any initialization logic.
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			PrepareSession ();
			session.StartRunning ();
		}

		public override void ViewDidLayoutSubviews ()
		{
			base.ViewDidLayoutSubviews ();
			previewLayer.Value.Frame = View.Frame;
			shapeLayer.Frame = View.Frame;
		}

		public override void ViewDidAppear (bool animated)
		{
			base.ViewDidAppear (animated);

			if (!previewLayer.IsValueCreated)
				return;

			View.Layer.AddSublayer (previewLayer.Value);

			shapeLayer.StrokeColor = UIColor.Red.CGColor;
			shapeLayer.LineWidth = 2;

			// Needs to filp coordinate system for Vision
			shapeLayer.AffineTransform = CGAffineTransform.MakeScale (-1, -1);
			View.Layer.AddSublayer (shapeLayer);
		}

		void PrepareSession ()
		{
			session = new AVCaptureSession ();
			var captureDevice = frontCamera;

			if (session == null || captureDevice == null)
				return;

			try {
				var deviceInput = new AVCaptureDeviceInput (captureDevice, out var deviceInputError);
				if (deviceInputError != null)
					throw new NSErrorException (deviceInputError);

				session.BeginConfiguration ();

				if (session.CanAddInput (deviceInput))
					session.AddInput (deviceInput);

				var output = new AVCaptureVideoDataOutput {
					UncompressedVideoSetting = new AVVideoSettingsUncompressed {
						PixelFormatType = CVPixelFormatType.CV420YpCbCr8BiPlanarFullRange
					},
					AlwaysDiscardsLateVideoFrames = true
				};

				if (session.CanAddOutput (output))
					session.AddOutput (output);

				session.CommitConfiguration ();

				var queue = new DispatchQueue ("output.queue");
				output.SetSampleBufferDelegateQueue (this, queue);

				Console.WriteLine ($"PrepareSession: Done setting up delegate");
			} catch (Exception ex) {
				Console.WriteLine ($"PrepareSession Error: {ex.Message}");
			}
		}

		[Export ("captureOutput:didOutputSampleBuffer:fromConnection:")]
		public void DidOutputSampleBuffer (AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
		{
			using (var pixelBuffer = sampleBuffer.GetImageBuffer ())
			using (var attachments = sampleBuffer.GetAttachments<NSString, NSObject> (CMAttachmentMode.ShouldPropagate))
			using (var ciimage = new CIImage (pixelBuffer, attachments))
			using (var ciImageWithOrientation = ciimage.CreateWithOrientation (CIImageOrientation.RightTop)) {
				DetectFace (ciImageWithOrientation);
			}
			// make sure we do not run out of sampleBuffers
			sampleBuffer.Dispose ();
		}

		void DetectFace (CIImage image)
		{
			faceDetectionRequest.Perform (new VNRequest [] { faceDetection }, image, out var performError);
			var results = faceDetection.GetResults<VNFaceObservation> () ?? Array.Empty<VNFaceObservation> ();
			if (results.Length > 0) {
				faceLandmarks.InputFaceObservations = results;
				DetectLandmarks (image);

				DispatchQueue.MainQueue.DispatchAsync (() => shapeLayer.Sublayers = Array.Empty<CALayer> ());
			}
		}

		void DetectLandmarks (CIImage image)
		{
			faceLandmarksDetectionRequest.Perform (new VNRequest [] { faceLandmarks }, image, out var performError);
			var landmarksResults = faceLandmarks?.GetResults<VNFaceObservation> () ?? Array.Empty<VNFaceObservation> ();
			foreach (var observation in landmarksResults) {
				DispatchQueue.MainQueue.DispatchAsync (() => {
					var boundingBox = faceLandmarks.InputFaceObservations.FirstOrDefault ()?.BoundingBox;
					if (boundingBox.HasValue) {
						//var faceBoundingBox = boundingBox.Value.Scale (View.Bounds.Size);

						// Different types of landmarks
						var faceContour = observation.Landmarks.FaceContour;
						//ConvertPoints (faceContour, faceBoundingBox);
						ConvertPoints2 (faceContour, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);

						var leftEye = observation.Landmarks.LeftEye;
						//ConvertPoints (leftEye, faceBoundingBox);
						ConvertPoints2 (leftEye, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);

						var rightEye = observation.Landmarks.RightEye;
						//ConvertPoints (rightEye, faceBoundingBox);
						ConvertPoints2 (rightEye, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);

						var leftEyebrow = observation.Landmarks.LeftEyebrow;
						//ConvertPoints (leftEyebrow, faceBoundingBox);
						ConvertPoints2 (leftEyebrow, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);

						var rightEyebrow = observation.Landmarks.RightEyebrow;
						//ConvertPoints (rightEyebrow, faceBoundingBox);
						ConvertPoints2 (rightEyebrow, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);

						var nose = observation.Landmarks.Nose;
						//ConvertPoints (nose, faceBoundingBox);
						ConvertPoints2 (nose, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);

						var noseCrest = observation.Landmarks.NoseCrest;
						//ConvertPoints (noseCrest, faceBoundingBox);
						ConvertPoints2 (noseCrest, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);

						var innerLips = observation.Landmarks.InnerLips;
						//ConvertPoints (innerLips, faceBoundingBox);
						ConvertPoints2 (innerLips, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);

						var outerLips = observation.Landmarks.OuterLips;
						ConvertPoints2 (outerLips, boundingBox.Value, (nuint) View.Bounds.Width, (nuint) View.Bounds.Height);
						//ConvertPoints (outerLips, faceBoundingBox);
					}
				});
			}
		}

		void ConvertPoints (VNFaceLandmarkRegion2D landmark, CGRect boundingBox)
		{
			var points = landmark.Points;
			var faceLandmarkPoints = points.Select (p => {
				return new CGPoint (
					x: ((nfloat) p.X) * boundingBox.Width + boundingBox.X,
					y: ((nfloat) p.Y) * boundingBox.Height + boundingBox.Y
				);
			});

			DispatchQueue.MainQueue.DispatchAsync (() => {
				Draw (faceLandmarkPoints.ToArray ());
			});
		}

		// Uses VNUtils to calculate the points, just to test the native API
		void ConvertPoints2 (VNFaceLandmarkRegion2D landmark, CGRect boundingBox, nuint imgWidth, nuint imgHeight)
		{
			var points = landmark.Points;
			var faceLandmarkPoints = points.Select (p =>
				VNUtils.GetImagePoint (p, boundingBox, imgWidth, imgHeight)
			);

			DispatchQueue.MainQueue.DispatchAsync (() => {
				Draw (faceLandmarkPoints.ToArray ());
			});
		}

		void Draw (CGPoint [] points)
		{
			var newLayer = new CAShapeLayer {
				StrokeColor = UIColor.Red.CGColor,
				LineWidth = 2
			};

			var path = new UIBezierPath ();
			path.MoveTo (points[0]);
			foreach (var point in points) {
				path.AddLineTo (point);
				path.MoveTo (point);
			}
			path.AddLineTo (points[0]);
			newLayer.Path = path.CGPath;

			shapeLayer.AddSublayer (newLayer);
		}
	}
}
