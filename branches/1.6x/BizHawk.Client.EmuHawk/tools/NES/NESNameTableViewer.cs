﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

using BizHawk.Client.Common;
using BizHawk.Emulation.Cores.Nintendo.NES;

namespace BizHawk.Client.EmuHawk
{
	public partial class NESNameTableViewer : Form, IToolForm
	{
		// TODO:
		// Show Scroll Lines + UI Toggle
		private readonly NES.PPU.DebugCallback _callback = new NES.PPU.DebugCallback();
		private NES _nes;

		public NESNameTableViewer()
		{
			InitializeComponent();
			Closing += (o, e) =>
				{
					Global.Config.NesNameTableSettings.Wndx = Location.X;
					Global.Config.NesNameTableSettings.Wndx = Location.Y;
					Global.Config.NESNameTableRefreshRate = RefreshRate.Value;
				};
			TopMost = Global.Config.NesNameTableSettings.TopMost;
			_callback.Callback = () => Generate();
		}

		private void NESNameTableViewer_Load(object sender, EventArgs e)
		{
			if (Global.Config.NesNameTableSettings.UseWindowPosition)
			{
				Location = Global.Config.NesNameTableSettings.WindowPosition;
			}

			_nes = Global.Emulator as NES;
			RefreshRate.Value = Global.Config.NESNameTableRefreshRate;
			Generate(true);
		}

		#region Public API

		public bool AskSave() { return true; }
		public bool UpdateBefore { get { return true; } }

		public void Restart()
		{
			if (Global.Emulator is NES)
			{
				_nes = Global.Emulator as NES;
				Generate(true);
			}
			else
			{
				Close();
			}
		}

		public void UpdateValues()
		{
			if (Global.Emulator is NES)
			{
				(Global.Emulator as NES).ppu.NTViewCallback = _callback;
			}
			else
			{
				Close();
			}
		}

		#endregion

		private unsafe void Generate(bool now = false)
		{
			if (!IsHandleCreated || IsDisposed)
			{
				return;
			}

			if (_nes == null)
			{
				return;
			}

			if (now == false && Global.Emulator.Frame % RefreshRate.Value != 0)
			{
				return;
			}

			var bmpdata = NameTableView.Nametables.LockBits(
				new Rectangle(0, 0, 512, 480),
				ImageLockMode.WriteOnly,
				PixelFormat.Format32bppArgb);

			var dptr = (int*)bmpdata.Scan0.ToPointer();
			var pitch = bmpdata.Stride / 4;
			var pt_add = _nes.ppu.reg_2000.bg_pattern_hi ? 0x1000 : 0;

			// Buffer all the data from the ppu, because it will be read multiple times and that is slow
			var ppuBuffer = new byte[0x3000];
			for (var i = 0; i < 0x3000; i++)
			{
				ppuBuffer[i] = _nes.ppu.ppubus_peek(i);
			}

			var palram = new byte[0x20];
			for (var i = 0; i < 0x20; i++)
			{
				palram[i] = _nes.ppu.PALRAM[i];
			}

			int ytable = 0, yline = 0;
			for (int y = 0; y < 480; y++)
			{
				if (y == 240)
				{
					ytable += 2;
					yline = 240;
				}
				for (int x = 0; x < 512; x++, dptr++)
				{
					int table = (x >> 8) + ytable;
					int ntaddr = table << 10;
					int px = x & 255;
					int py = y - yline;
					int tx = px >> 3;
					int ty = py >> 3;
					int ntbyte_ptr = ntaddr + (ty * 32) + tx;
					int atbyte_ptr = ntaddr + 0x3C0 + ((ty >> 2) << 3) + (tx >> 2);
					int nt = ppuBuffer[ntbyte_ptr + 0x2000];

					int at = ppuBuffer[atbyte_ptr + 0x2000];
					if ((ty & 2) != 0)
					{
						at >>= 4;
					}

					if ((tx & 2) != 0)
					{
						at >>= 2;
					}

					at &= 0x03;
					at <<= 2;

					int bgpx = x & 7;
					int bgpy = y & 7;
					int pt_addr = (nt << 4) + bgpy + pt_add;
					int pt_0 = ppuBuffer[pt_addr];
					int pt_1 = ppuBuffer[pt_addr + 8];
					int pixel = ((pt_0 >> (7 - bgpx)) & 1) | (((pt_1 >> (7 - bgpx)) & 1) << 1);

					// if the pixel is transparent, draw the backdrop color
					// TODO - consider making this optional? nintendulator does it and fceux doesnt need to do it due to buggy palette logic which creates the same effect
					if (pixel != 0)
					{
						pixel |= at;
					}

					pixel = palram[pixel];
					*dptr = _nes.LookupColor(pixel);
				}

				dptr += pitch - 512;
			}

			NameTableView.Nametables.UnlockBits(bmpdata);
			NameTableView.Refresh();
		}

		private void RefreshFloatingWindowControl()
		{
			Owner = Global.Config.NesNameTableSettings.FloatingWindow ? null : GlobalWin.MainForm;
		}

		#region Events

		#region Menu and Context Menu

		private void ScreenshotMenuItem_Click(object sender, EventArgs e)
		{
			NameTableView.Screenshot();
		}

		private void ScreenshotToClipboardMenuItem_Click(object sender, EventArgs e)
		{
			NameTableView.ScreenshotToClipboard();
		}

		private void ExitMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void OptionsSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			AutoloadMenuItem.Checked = Global.Config.AutoLoadNESNameTable;
			SaveWindowPositionMenuItem.Checked = Global.Config.NesNameTableSettings.SaveWindowPosition;
			AlwaysOnTopMenuItem.Checked = Global.Config.NesNameTableSettings.TopMost;
			FloatingWindowMenuItem.Checked = Global.Config.NesNameTableSettings.FloatingWindow;
		}

		private void AutoloadMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.AutoLoadNESNameTable ^= true;
		}

		private void SaveWindowPositionMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.NesNameTableSettings.SaveWindowPosition ^= true;
		}

		private void AlwaysOnTopMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.NesNameTableSettings.TopMost ^= true;
			TopMost = Global.Config.NesNameTableSettings.TopMost;
		}

		private void FloatingWindowMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.NesNameTableSettings.FloatingWindow ^= true;
			RefreshFloatingWindowControl();
		}

		private void RefreshImageContextMenuItem_Click(object sender, EventArgs e)
		{
			UpdateValues();
			NameTableView.Refresh();
		}

		#endregion

		#region Dialog and Controls

		private void NesNameTableViewer_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.C:
					if (e.Modifiers == Keys.Control)
					{
						NameTableView.ScreenshotToClipboard();
					}

					break;
			}
		}

		protected override void OnShown(EventArgs e)
		{
			RefreshFloatingWindowControl();
			base.OnShown(e);
		}

		private void NESNameTableViewer_FormClosed(object sender, FormClosedEventArgs e)
		{
			if (_nes != null && _nes.ppu.NTViewCallback == _callback)
			{
				_nes.ppu.NTViewCallback = null;
			}
		}

		private void ScanlineTextbox_TextChanged(object sender, EventArgs e)
		{
			int temp;
			if (int.TryParse(txtScanline.Text, out temp))
			{
				_callback.Scanline = temp;
			}
		}

		private void NametableRadio_CheckedChanged(object sender, EventArgs e)
		{
			if (rbNametableNW.Checked)
			{
				NameTableView.Which = NameTableViewer.WhichNametable.NT_2000;
			}

			if (rbNametableNE.Checked)
			{
				NameTableView.Which = NameTableViewer.WhichNametable.NT_2400;
			}

			if (rbNametableSW.Checked)
			{
				NameTableView.Which = NameTableViewer.WhichNametable.NT_2800;
			}

			if (rbNametableSE.Checked)
			{
				NameTableView.Which = NameTableViewer.WhichNametable.NT_2C00;
			}

			if (rbNametableAll.Checked)
			{
				NameTableView.Which = NameTableViewer.WhichNametable.NT_ALL;
			}
		}

		private void NameTableView_MouseMove(object sender, MouseEventArgs e)
		{
			int TileX, TileY, NameTable;
			if (NameTableView.Which == NameTableViewer.WhichNametable.NT_ALL)
			{
				TileX = e.X / 8;
				TileY = e.Y / 8;
				NameTable = (TileX / 32) + ((TileY / 30) * 2);
			}
			else
			{
				switch (NameTableView.Which)
				{
					default:
					case NameTableViewer.WhichNametable.NT_2000:
						NameTable = 0;
						break;
					case NameTableViewer.WhichNametable.NT_2400:
						NameTable = 1;
						break;
					case NameTableViewer.WhichNametable.NT_2800:
						NameTable = 2;
						break;
					case NameTableViewer.WhichNametable.NT_2C00:
						NameTable = 3;
						break;
				}

				TileX = e.X / 16;
				TileY = e.Y / 16;
			}

			XYLabel.Text = TileX + " : " + TileY;
			int PPUAddress = 0x2000 + (NameTable * 0x400) + ((TileY % 30) * 32) + (TileX % 32);
			PPUAddressLabel.Text = string.Format("{0:X4}", PPUAddress);
			int TileID = _nes.ppu.ppubus_read(PPUAddress, true);
			TileIDLabel.Text = string.Format("{0:X2}", TileID);
			TableLabel.Text = NameTable.ToString();

			int ytable = 0, yline = 0;
			if (e.Y >= 240)
			{
				ytable += 2;
				yline = 240;
			}
			int table = (e.X >> 8) + ytable;
			int ntaddr = (table << 10);
			int px = e.X & 255;
			int py = e.Y - yline;
			int tx = px >> 3;
			int ty = py >> 3;
			int atbyte_ptr = ntaddr + 0x3C0 + ((ty >> 2) << 3) + (tx >> 2);
			int at = _nes.ppu.ppubus_peek(atbyte_ptr + 0x2000);
			if ((ty & 2) != 0) at >>= 4;
			if ((tx & 2) != 0) at >>= 2;
			at &= 0x03;
			PaletteLabel.Text = at.ToString();
		}

		private void NameTableView_MouseLeave(object sender, EventArgs e)
		{
			XYLabel.Text = string.Empty;
			PPUAddressLabel.Text = string.Empty;
			TileIDLabel.Text = string.Empty;
			TableLabel.Text = string.Empty;
			PaletteLabel.Text = string.Empty;
		}

		#endregion

		#endregion
	}
}
