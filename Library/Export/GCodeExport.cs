﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.Library.Export
{
	public class GCodeExport : IExportPlugin
	{
		public string ButtonText => "G-Code File".Localize();

		public string FileExtension => ".gcode";

		public string ExtensionFilter => "Export GCode|*.gcode";

		public bool EnabledForCurrentPart(ILibraryContentStream libraryContent)
		{
			return !libraryContent.IsProtected;
		}

		public GuiWidget GetOptionsPanel()
		{
			// If print leveling is enabled then add in a check box 'Apply Leveling During Export' and default checked.
			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
			{
				var container = new FlowLayoutWidget();

				var checkbox = new CheckBox("Apply leveling to G-Code during export".Localize(), ActiveTheme.Instance.PrimaryTextColor, 10)
				{
					Checked = true,
					Cursor = Cursors.Hand,
				};
				checkbox.CheckedStateChanged += (s, e) =>
				{
					this.ApplyLeveling = checkbox.Checked;
				};
				container.AddChild(checkbox);

				return container;
			}

			return null;
		}

		public async Task<bool> Generate(IEnumerable<ILibraryItem> libraryItems, string outputPath)
		{
			ILibraryContentStream libraryContent = libraryItems.OfType<ILibraryContentStream>().FirstOrDefault();

			if (libraryContent != null)
			{
				try
				{
					string newGCodePath = await SliceFileIfNeeded(libraryContent);

					if (File.Exists(newGCodePath))
					{
						SaveGCodeToNewLocation(newGCodePath, outputPath);
						return true;
					}
				}
				catch
				{
				}

			}

			return false;

		}

		// partIsGCode = Path.GetExtension(libraryContent.FileName).ToUpper() == ".GCODE";


		private async Task<string> SliceFileIfNeeded(ILibraryContentStream libraryContent)
		{
			// TODO: How to handle gcode files in library content?
			//string fileToProcess = partIsGCode ?  printItemWrapper.FileLocation : "";
			string fileToProcess = "";

			string sourceExtension = Path.GetExtension(libraryContent.FileName).ToUpper();
			if (MeshFileIo.ValidFileExtensions().Contains(sourceExtension)
				|| sourceExtension == ".MCX")
			{
				// Save any pending changes before starting the print
				await ApplicationController.Instance.ActiveView3DWidget.PersistPlateIfNeeded();

				var printItem = ApplicationController.Instance.ActivePrintItem;

				await SlicingQueue.SliceFileAsync(printItem, null);

				fileToProcess = printItem.GetGCodePathAndFileName();
			}

			return fileToProcess;
		}

		public bool ApplyLeveling { get; set; }

		private void SaveGCodeToNewLocation(string gcodeFilename, string dest)
		{
			try
			{
				GCodeFileStream gCodeFileStream = new GCodeFileStream(GCodeFile.Load(gcodeFilename, CancellationToken.None));

				bool addLevelingStream = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled) && this.ApplyLeveling;
				var queueStream = new QueuedCommandsStream(gCodeFileStream);

				// this is added to ensure we are rewriting the G0 G1 commands as needed
				GCodeStream finalStream = addLevelingStream
					? new ProcessWriteRegexStream(new PrintLevelingStream(queueStream, false), queueStream)
					: new ProcessWriteRegexStream(queueStream, queueStream);

				using (StreamWriter file = new StreamWriter(dest))
				{
					string nextLine = finalStream.ReadLine();
					while (nextLine != null)
					{
						if (nextLine.Trim().Length > 0)
						{
							file.WriteLine(nextLine);
						}
						nextLine = finalStream.ReadLine();
					}
				}
			}
			catch (Exception e)
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(null, e.Message, "Couldn't save file".Localize());
				});
			}
		}


	}
}