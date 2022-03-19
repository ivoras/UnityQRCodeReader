/////////////////////////////////////////////////////////////////////
//
//	QR Code Library
//
//	QR Code three finders corner class.
//
//	Author: Uzi Granot
//
//	Current Version: 3.1.0
//	Date: March 7, 2022
//
//	Original Version: 1.0
//	Date: June 30, 2018
//
//	Copyright (C) 2018-2022 Uzi Granot. All Rights Reserved
//
//	QR Code Library C# class library and the attached test/demo
//  applications are free software.
//	Software developed by this author is licensed under CPOL 1.02.
//	Some portions of the QRCodeVideoDecoder are licensed under GNU Lesser
//	General Public License v3.0.
//
//	The video decoder is using some of the source modules of
//	Camera_Net project published at CodeProject.com:
//	https://www.codeproject.com/Articles/671407/Camera_Net-Library
//	and at GitHub: https://github.com/free5lot/Camera_Net.
//	This project is based on DirectShowLib.
//	http://sourceforge.net/projects/directshownet/
//	This project includes a modified subset of the source modules.
//
//	The main points of CPOL 1.02 subject to the terms of the License are:
//
//	Source Code and Executable Files can be used in commercial applications;
//	Source Code and Executable Files can be redistributed; and
//	Source Code can be modified to create derivative works.
//	No claim of suitability, guarantee, or any warranty whatsoever is
//	provided. The software is provided "as-is".
//	The Article accompanying the Work may not be distributed or republished
//	without the Author's consent
//
//	For version history please refer to QRDecoder.cs
/////////////////////////////////////////////////////////////////////
using System;
using UnityEngine;

namespace QRCodeDecoderLibrary
	{
	/////////////////////////////////////////////////////////////////////
	// QR corner three finders pattern class
	/////////////////////////////////////////////////////////////////////

	internal class QRCodeCorner
		{
		internal QRCodeFinder TopLeftFinder;
		internal QRCodeFinder TopRightFinder;
		internal QRCodeFinder BottomLeftFinder;

		internal float TopLineDeltaX;
		internal float TopLineDeltaY;
		internal float TopLineLength;
		internal float LeftLineDeltaX;
		internal float LeftLineDeltaY;
		internal float LeftLineLength;

		/////////////////////////////////////////////////////////////////////
		// QR corner constructor
		/////////////////////////////////////////////////////////////////////

		private QRCodeCorner
				(
				QRCodeFinder TopLeftFinder,
				QRCodeFinder TopRightFinder,
				QRCodeFinder BottomLeftFinder
				)
			{
			// save three finders
			this.TopLeftFinder = TopLeftFinder;
			this.TopRightFinder = TopRightFinder;
			this.BottomLeftFinder = BottomLeftFinder;

			// top line slope
			TopLineDeltaX = TopRightFinder.Col - TopLeftFinder.Col;
			TopLineDeltaY = TopRightFinder.Row - TopLeftFinder.Row;

			// top line length
			TopLineLength = Mathf.Sqrt(TopLineDeltaX * TopLineDeltaX + TopLineDeltaY * TopLineDeltaY);

			// left line slope
			LeftLineDeltaX = BottomLeftFinder.Col - TopLeftFinder.Col;
			LeftLineDeltaY = BottomLeftFinder.Row - TopLeftFinder.Row;

			// left line length
			LeftLineLength = Mathf.Sqrt(LeftLineDeltaX * LeftLineDeltaX + LeftLineDeltaY * LeftLineDeltaY);
			return;
			}

		/////////////////////////////////////////////////////////////////////
		// Test QR corner for validity
		/////////////////////////////////////////////////////////////////////

		internal static QRCodeCorner CreateCorner
				(
				QRCodeFinder TopLeftFinder,
				QRCodeFinder TopRightFinder,
				QRCodeFinder BottomLeftFinder
				)
			{
			// try all three possible permutation of three finders
			for(int Index = 0; Index < 3; Index++)
				{
				// TestCorner runs three times to test all posibilities
				// rotate top left, top right and bottom left
				if(Index != 0)
					{
					QRCodeFinder Temp = TopLeftFinder;
					TopLeftFinder = TopRightFinder;
					TopRightFinder = BottomLeftFinder;
					BottomLeftFinder = Temp;
					}

				// top line slope
				float TopLineDeltaX = TopRightFinder.Col - TopLeftFinder.Col;
				float TopLineDeltaY = TopRightFinder.Row - TopLeftFinder.Row;

				// left line slope
				float LeftLineDeltaX = BottomLeftFinder.Col - TopLeftFinder.Col;
				float LeftLineDeltaY = BottomLeftFinder.Row - TopLeftFinder.Row;

				// top line length
				float TopLineLength = Mathf.Sqrt(TopLineDeltaX * TopLineDeltaX + TopLineDeltaY * TopLineDeltaY);

				// left line length
				float LeftLineLength = Mathf.Sqrt(LeftLineDeltaX * LeftLineDeltaX + LeftLineDeltaY * LeftLineDeltaY);

				// the short side must be at least 80% of the long side
				if(Mathf.Min(TopLineLength, LeftLineLength) < QRDecoder.CORNER_SIDE_LENGTH_DEV * Mathf.Max(TopLineLength, LeftLineLength))
					continue;

				// top line vector
				float TopLineSin = TopLineDeltaY / TopLineLength;
				float TopLineCos = TopLineDeltaX / TopLineLength;

				// rotate lines such that top line is parallel to x axis
				// left line after rotation
				float NewLeftX = TopLineCos * LeftLineDeltaX + TopLineSin * LeftLineDeltaY;
				float NewLeftY = -TopLineSin * LeftLineDeltaX + TopLineCos * LeftLineDeltaY;

				// new left line X should be zero (or between +/- 4 deg)
				if(Mathf.Abs(NewLeftX / LeftLineLength) > QRDecoder.CORNER_RIGHT_ANGLE_DEV)
					continue;

				// swap top line with left line
				if(NewLeftY < 0)
					{
					// swap top left with bottom right
					QRCodeFinder TempFinder = TopRightFinder;
					TopRightFinder = BottomLeftFinder;
					BottomLeftFinder = TempFinder;
					}

				return new QRCodeCorner(TopLeftFinder, TopRightFinder, BottomLeftFinder);
				}
			return null;
			}

		/////////////////////////////////////////////////////////////////////
		// Test QR corner for validity
		/////////////////////////////////////////////////////////////////////

		internal int InitialVersionNumber()
			{
			// version number based on top line
			float TopModules = 7;

			// top line is mostly horizontal
			if(Mathf.Abs(TopLineDeltaX) >= Mathf.Abs(TopLineDeltaY))
				{
				TopModules += TopLineLength * TopLineLength /
					(Mathf.Abs(TopLineDeltaX) * 0.5f * (TopLeftFinder.HModule + TopRightFinder.HModule));
				}

			// top line is mostly vertical
			else
				{
				TopModules += TopLineLength * TopLineLength /
					(Mathf.Abs(TopLineDeltaY) * 0.5f * (TopLeftFinder.VModule + TopRightFinder.VModule));
				}

			// version number based on left line
			float LeftModules = 7;

			// Left line is mostly vertical
			if(Mathf.Abs(LeftLineDeltaY) >= Mathf.Abs(LeftLineDeltaX))
				{
				LeftModules += LeftLineLength * LeftLineLength /
					(Mathf.Abs(LeftLineDeltaY) * 0.5f * (TopLeftFinder.VModule + BottomLeftFinder.VModule));
				}

			// left line is mostly horizontal
			else
				{
				LeftModules += LeftLineLength * LeftLineLength /
					(Mathf.Abs(LeftLineDeltaX) * 0.5f * (TopLeftFinder.HModule + BottomLeftFinder.HModule));
				}

			// version (there is rounding in the calculation)
			int Version = ((int) Mathf.Round(0.5f * (TopModules + LeftModules)) - 15) / 4;

			// not a valid corner
			if(Version < 1 || Version > 40)
				throw new Exception("Corner is not valid (version number must be 1 to 40)");

			// exit with version number
			return Version;
			}
		}
	}
