﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.Text.Editor;
using dnSpy.Contracts.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace dnSpy.Text.Editor {
	[Export(typeof(ILineTransformSourceProvider))]
	[ContentType(ContentTypes.Any)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
	[TextViewRole(PredefinedTextViewRoles.PreviewTextView)]
	[TextViewRole(PredefinedTextViewRoles.Printable)]
	[TextViewRole(PredefinedDnSpyTextViewRoles.CanHaveLineCompressor)]
	sealed class LineCompressorProvider : ILineTransformSourceProvider {
		public ILineTransformSource Create(IWpfTextView textView) => new LineCompressor(textView);
	}

	sealed class LineCompressor : ILineTransformSource {
		readonly IWpfTextView textView;
		bool compressEmptyOrWhitespaceLines;
		bool compressNonLetterLines;
		const double SCALE = 0.75;
		const int MAX_LINE_LENGTH = 150;

		enum LineKind {
			Normal,
			EmptyOrWhitespace,
			NoLettersDigits,
		}

		public LineCompressor(IWpfTextView textView) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			this.textView = textView;
			textView.Closed += TextView_Closed;
			textView.Options.OptionChanged += Options_OptionChanged;
			InitializeOptions(false);
		}

		void InitializeOptions(bool refresh) {
			compressEmptyOrWhitespaceLines = textView.Options.IsCompressEmptyOrWhitespaceLinesEnabled();
			compressNonLetterLines = textView.Options.IsCompressNonLetterLinesEnabled();
			if (refresh) {
				var line = textView.TextViewLines.FirstVisibleLine;
				textView.DisplayTextLineContainingBufferPosition(line.Start, line.Top - textView.ViewportTop, ViewRelativePosition.Top);
			}
		}

		void Options_OptionChanged(object sender, EditorOptionChangedEventArgs e) {
			if (e.OptionId == DefaultDnSpyTextViewOptions.CompressEmptyOrWhitespaceLinesId.Name || e.OptionId == DefaultDnSpyTextViewOptions.CompressNonLetterLinesId.Name)
				InitializeOptions(true);
		}

		public LineTransform GetLineTransform(ITextViewLine line, double yPosition, ViewRelativePosition placement) {
			if (!compressEmptyOrWhitespaceLines && !compressNonLetterLines)
				return line.DefaultLineTransform;
			if (!line.IsFirstTextViewLineForSnapshotLine && !line.IsLastTextViewLineForSnapshotLine)
				return line.DefaultLineTransform;

			switch (GetLineType(line)) {
			case LineKind.Normal:
				return line.DefaultLineTransform;
			case LineKind.EmptyOrWhitespace:
				if (compressEmptyOrWhitespaceLines)
					return new LineTransform(0, 0, SCALE, 0);
				return line.DefaultLineTransform;
			case LineKind.NoLettersDigits:
				if (compressNonLetterLines)
					return new LineTransform(0, 0, SCALE, 0);
				return line.DefaultLineTransform;
			default:
				throw new InvalidOperationException();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		LineKind GetLineType(ITextViewLine line) {
			if (line.Length == 0)
				return LineKind.EmptyOrWhitespace;
			var snapshot = line.Start.Snapshot;
			var c = snapshot[line.Start.Position + line.Length / 2];
			if (char.IsLetterOrDigit(c))
				return LineKind.Normal;
			if (line.Length > MAX_LINE_LENGTH)
				return LineKind.Normal;
			int end = line.End.Position;
			bool isBlank = true;
			for (int pos = line.Start.Position; pos < end; pos++) {
				c = snapshot[pos];
				if (char.IsLetterOrDigit(c))
					return LineKind.Normal;
				if (isBlank && !char.IsWhiteSpace(c))
					isBlank = false;
			}
			return isBlank ? LineKind.EmptyOrWhitespace : LineKind.NoLettersDigits;
		}

		void TextView_Closed(object sender, EventArgs e) {
			textView.Closed -= TextView_Closed;
			textView.Options.OptionChanged -= Options_OptionChanged;
		}
	}
}