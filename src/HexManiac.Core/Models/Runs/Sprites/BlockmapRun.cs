﻿using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using static IronPython.Modules._ast;

/*
 layout<[
   width:: height::
   borderblock<>
   map<>
   tiles1<>
   tiles2<>
   borderwidth. borderheight. unused:]1>
 */

namespace HexManiac.Core.Models.Runs.Sprites {
   public class BlockmapRun : BaseRun {
      private readonly IDataModel model;

      private static int TotalTiles => 1024;
      private static int TotalPalettes => 13;

      public int BlockWidth { get; }
      public int BlockHeight { get; }

      public int PrimaryTiles { get; }
      public int PrimaryPalettes { get; }

      public override int Length => BlockWidth * BlockHeight * 2;

      public static string SharedFormatString => "`blm`";
      public override string FormatString => SharedFormatString;

      public BlockmapRun(IDataModel model, int start, SortedSpan<int> sources, int width = -1, int height = -1) : base(start, sources) {
         this.model = model;
         if (sources != null && sources.Count > 0) {
            var primarySource = sources[0];
            var layoutStart = primarySource - 12;
            if (width == -1) width = model.ReadValue(layoutStart);
            if (height == -1) height = model.ReadValue(layoutStart + 4);
         }
         (BlockWidth, BlockHeight) = (width, height);
         var code = model.GetGameCode();

         if (code.Contains("BPRE") || code.Contains("BPGE")) {
            PrimaryTiles = 640;
            PrimaryPalettes = 7;
         } else {
            PrimaryTiles = 512;
            PrimaryPalettes = 6;
         }
      }

      public static short[][] ReadPalettes(BlocksetModel blockModel1, BlocksetModel blockModel2, int primaryPalettes) {
         var primary = blockModel1.ReadPalettes();
         var secondary = blockModel2.ReadPalettes();
         if (primary == null || secondary == null) return null;
         var result = new short[TotalPalettes][];
         for (int i = 0; i < primaryPalettes; i++) result[i] = primary[i];
         for (int i = primaryPalettes; i < TotalPalettes; i++) result[i] = secondary[i];
         return result;
      }

      public static void WritePalettes(ModelDelta token, BlocksetModel blockModel1, BlocksetModel blockModel2, int primaryPalettes, short[][] palettes) {
         var primary = new List<short[]>();
         var secondary = new List<short[]>();
         for (int i = 0; i < primaryPalettes; i++) {
            secondary.Add(new short[16]);
            primary.Add(palettes[i]);
         }
         for (int i = primaryPalettes; i < TotalPalettes; i++) secondary.Add(palettes[i]);
         blockModel1.WritePalettes(primary.ToArray(), token);
         blockModel2.WritePalettes(secondary.ToArray(), token); // TODO only write the secondary palettes, not the filler palettes
      }

      public static int[][,] ReadTiles(BlocksetModel blockModel1, BlocksetModel blockModel2, int primaryTiles) {
         var primary = blockModel1.ReadTiles();
         var secondary = blockModel2.ReadTiles();
         if (primary == null || secondary == null) return null;
         var result = new int[TotalTiles][,];
         for (int i = 0; i < primaryTiles && i < primary.Length; i++) result[i] = primary[i];
         for (int i = primaryTiles; i < TotalTiles && i < secondary.Length + primaryTiles; i++) result[i] = secondary[i - primaryTiles];
         return result;
      }

      public static List<int> WriteTiles(ModelDelta token, BlocksetModel blockModel1, BlocksetModel blockModel2, int primaryTiles, int[][,] tiles) {
         var primary = new List<int[,]>();
         var secondary = new List<int[,]>();
         for (int i = 0; i < primaryTiles; i++) primary.Add(tiles[i]);
         for (int i = primaryTiles; i < tiles.Length; i++) secondary.Add(tiles[i]);
         var a = blockModel1.WriteTiles(primary.ToArray(), token);
         var b = blockModel2.WriteTiles(secondary.ToArray(), token);
         return new List<int> { a, b };
      }

      public static byte[][] ReadBlocks(BlocksetModel blockModel1, BlocksetModel blockModel2) {
         var primary = blockModel1.ReadBlocks();
         var secondary = blockModel2.ReadBlocks();
         var result = new List<byte[]>();
         for (int i = 0; i < primary.Length; i++) result.Add(primary[i]);
         while (result.Count < blockModel1.PrimaryBlocks) result.Add(new byte[16]);
         for (int i = 0; i < secondary.Length; i++) result.Add(secondary[i]);
         return result.ToArray();
      }

      public static void WriteBlocks(ModelDelta token, BlocksetModel blockModel1, BlocksetModel blockModel2, byte[][] blocks) {
         var primary = new List<byte[]>();
         var secondary = new List<byte[]>();
         for (int i = 0; i < blockModel1.PrimaryBlocks; i++) primary.Add(blocks[i]);
         for (int i = 0; i < blocks.Length - blockModel1.PrimaryBlocks; i++) secondary.Add(blocks[i + blockModel1.PrimaryBlocks]);
         blockModel1.WriteBlocks(primary.ToArray(), token);
         blockModel2.WriteBlocks(secondary.ToArray(), token);
      }

      public static byte[][] ReadBlockAttributes(BlocksetModel blockModel1, BlocksetModel blockModel2) {
         var primary = blockModel1.ReadBlockAttributes();
         var secondary = blockModel2.ReadBlockAttributes();
         var result = new List<byte[]>();
         for (int i = 0; i < primary.Length; i++) result.Add(primary[i]);
         while (result.Count < blockModel1.PrimaryBlocks) result.Add(new byte[blockModel1.BytesPerAttribute]);
         for (int i = 0; i < secondary.Length; i++) result.Add(secondary[i]);
         return result.ToArray();
      }

      public static void WriteBlockAttributes(ModelDelta token, BlocksetModel blockModel1, BlocksetModel blockModel2, byte[][] blockAttributes) {
         var primary = new List<byte[]>();
         var secondary = new List<byte[]>();
         for (int i = 0; i < blockModel1.PrimaryBlocks; i++) primary.Add(blockAttributes[i]);
         for (int i = 0; i < blockAttributes.Length - blockModel1.PrimaryBlocks; i++) secondary.Add(blockAttributes[i + blockModel1.PrimaryBlocks]);
         blockModel1.WriteBlockAttributes(primary.ToArray(), token);
         blockModel2.WriteBlockAttributes(secondary.ToArray(), token);
      }

      public static IEnumerable<IPixelViewModel> CalculateBlockRenders(byte[][] blocks, int[][,] tiles, short[][] palettes) {
         for (int i = 0; i < blocks.Length; i++) {
            yield return BlocksetModel.RenderBlock(blocks[i], tiles, palettes);
         }
      }

      public static CanvasPixelViewModel RenderMap(IReadOnlyList<byte> model, int start, int width, int height, IReadOnlyList<IPixelViewModel> blocks) {
         var canvas = new CanvasPixelViewModel(width * 16, height * 16);
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
               var value = model.ReadMultiByteValue(start + (y * width + x) * 2, 2) & 0x3FF;
               canvas.Draw(blocks[value], x * 16, y * 16);
            }
         }
         return canvas;
      }

      int lastFormatRequested = int.MaxValue;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         IDataFormat basicFormat;
         if (index % 2 == 0) {
            basicFormat = new IntegerHex(index, 0, data.ReadMultiByteValue(index, 2), 2);
         } else {
            basicFormat = new IntegerHex(index - 1, 1, data.ReadMultiByteValue(index - 1, 2), 2);
         }
         if (!CreateForLeftEdge) return basicFormat;
         if (lastFormatRequested < index) {
            lastFormatRequested = index;
            return basicFormat;
         }

         var sprite = data.CurrentCacheScope.GetImage(this);
         var availableRows = (Length - (index - Start)) / ExpectedDisplayWidth;
         lastFormatRequested = index;
         return new SpriteDecorator(basicFormat, sprite, ExpectedDisplayWidth, availableRows);
      }

      public BlockmapRun TryChangeSize(Func<ModelDelta> tokenFactory, MapDirection direction, int amount) {
         if (amount == 0) return null;

         int xAmount = 0, yAmount = 0;
         if (direction == MapDirection.Left || direction == MapDirection.Right) xAmount = amount;
         if (direction == MapDirection.Up || direction == MapDirection.Down) yAmount = amount;
         var (newWidth, newHeight) = (BlockWidth + xAmount, BlockHeight + yAmount);

         // validate that the new width/height combo is reasonable
         if (amount > 0 && !BlockMapViewModel.IsMapWithinSizeLimit(newWidth, newHeight)) return null;
         if (amount < 0 && (newWidth < 4 || newHeight < 4)) return null;

         var data = new int[BlockWidth, BlockHeight];
         for (int y = 0; y < BlockHeight; y++) {
            for (int x = 0; x < BlockWidth; x++) {
               data[x, y] = model.ReadMultiByteValue(Start + (y * BlockWidth + x) * 2, 2);
            }
         }

         // copy data into new array
         int xOffset = direction == MapDirection.Left ? amount : 0;
         int yOffset = direction == MapDirection.Up ? amount : 0;
         var newData = new int[BlockWidth + xAmount, BlockHeight + yAmount];
         for (int y = 0; y < BlockHeight; y++) {
            if (y + yOffset < 0) continue;
            if (y + yOffset >= newHeight) continue;
            for (int x = 0; x < BlockWidth; x++) {
               if (x + xOffset < 0) continue;
               if (x + xOffset >= newWidth) continue;
               newData[x + xOffset, y + yOffset] = data[x, y];
            }
         }

         // fill new rows/columns
         for (int y = yOffset - 1; y >= 0; y--) {
            for (int x = 0; x < newWidth; x++) newData[x, y] = newData[x, y + 1];
         }
         if (yOffset == 0) {
            for (int y = BlockHeight; y < newHeight; y++) {
               for (int x = 0; x < newWidth; x++) newData[x, y] = newData[x, y - 1];
            }
         }
         for (int x = xOffset - 1; x >= 0; x--) {
            for (int y = 0; y < newHeight; y++) newData[x, y] = newData[x + 1, y];
         }
         if (xOffset == 0) {
            for (int x = BlockWidth; x < newWidth; x++) {
               for (int y = 0; y < newHeight; y++) newData[x, y] = newData[x - 1, y];
            }
         }

         // copy data to model
         var token = tokenFactory();
         var run = model.RelocateForExpansion(token, this, newData.Length * 2);
         for (int y = 0; y < newHeight; y++) {
            for (int x = 0; x < newWidth; x++) {
               model.WriteMultiByteValue(run.Start + (y * newWidth + x) * 2, 2, token, newData[x, y]);
            }
         }
         var primarySource = PointerSources[0];
         var layoutStart = primarySource - 12;
         model.WriteValue(token, layoutStart, newWidth);
         model.WriteValue(token, layoutStart + 4, newHeight);
         var newRun = new BlockmapRun(model, run.Start, run.PointerSources, newWidth, newHeight);
         model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new BlockmapRun(model, Start, newPointerSources, BlockWidth, BlockHeight);
   }

   public class BlocksetModel {
      private readonly IDataModel model;
      private readonly int address;
      private readonly int primaryBlocks, primaryTiles, primaryPalettes, attributeOffset;

      public int PrimaryBlocks => primaryBlocks;
      public int BytesPerAttribute { get; }

      public BlocksetModel(IDataModel model, int address) {
         this.model = model;
         this.address = address;
         if (model.IsFRLG()) {
            primaryBlocks = 640;
            primaryTiles = 640;
            primaryPalettes = 7;
            attributeOffset = 20;
            BytesPerAttribute = 4;
         } else {
            primaryBlocks = 512;
            primaryTiles = 512;
            primaryPalettes = 6;
            attributeOffset = 16;
            BytesPerAttribute = 2;
         }
      }

      int Read(int offset) => model[address + offset];
      int ReadPointer(int offset) => model.ReadPointer(address + offset);

      public bool IsCompressed => Read(0) == 1;
      public bool IsSecondary => Read(1) == 1;

      public int[][,] ReadTiles() {
         if (!IsCompressed) return ReadUncompressedTiles();
         int start = ReadPointer(4);
         if (start < 0 || start > model.Count) return null;
         var run = new LzTilesetRun(new TilesetFormat(4, null), model, start);
         var fullData = run.GetPixels(model, 0, 1);
         var list = new List<int[,]>();
         for (int i = 0; i < fullData.GetLength(1) / 8; i++) {
            var tile = new int[8, 8];
            for (int x = 0; x < 8; x++) {
               for (int y = 0; y < 8; y++) {
                  tile[x, y] = fullData[x, y + i * 8];
               }
            }
            list.Add(tile);
         }
         return list.ToArray();
      }

      public int WriteTiles(int[][,] tiles, ModelDelta token) {
         if (!IsCompressed) {
            return WriteUncompressedTiles(tiles, token);
         }
         // TODO make sure this repoints correctly even when there's no LzRun for the data
         var start = ReadPointer(4);
         var run = new LzTilesetRun(new TilesetFormat(4, null), model, start);
         run = run.SetPixels(model, token, tiles);
         return run.Start;
      }

      private int[][,] ReadUncompressedTiles() {
         var list = new List<int[,]>();
         var start = ReadPointer(4);
         var tileCount = !IsSecondary ? primaryTiles : 1024 - primaryTiles;
         EstimateTileCount(ref tileCount, start);
         for (int i = 0; i < tileCount; i++) {
            var tile = new int[8, 8];
            for(int y = 0; y < 8; y++) {
               for (int x = 0; x < 4; x++) {
                  tile[x * 2 + 0, y] = model[start + i * 32 + y * 4 + x] & 0xF;
                  tile[x * 2 + 1, y] = model[start + i * 32 + y * 4 + x] >> 4;
               }
            }
            list.Add(tile);
         }
         return list.ToArray();
      }

      private int WriteUncompressedTiles(int[][,] tiles, ModelDelta token) {
         // TODO this current doesn't worry about repointing or expansion, but it probably should
         var start = ReadPointer(4);
         for (int i = 0; i < tiles.Length; i++) {
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 4; x++) {
                  var low = tiles[i][x * 2 + 0, y];
                  var high = tiles[i][x * 2 + 1, y];
                  token.ChangeData(model, start + i * 32 + y * 4 + x, (byte)((high << 4) | low));
               }
            }
         }
         return start;
      }

      // TODO verify that there are always 16 palettes to read, even though we won't use them all
      public short[][] ReadPalettes() {
         int start = ReadPointer(8);
         if (start < 0 || start >= model.Count) return null;
         var data = new short[16][];
         for (int i = 0; i < 16; i++) {
            data[i] = new short[16];
            for (int j = 0; j < 16; j++) {
               var bgr = (short)model.ReadMultiByteValue(start + i * 32 + j * 2, 2);
               data[i][j] = PaletteRun.FlipColorChannels(bgr);
            }
         }
         return data;
      }

      public void WritePalettes(short[][] palettes, ModelDelta token) {
         var start = ReadPointer(8);
         for (int i = 0; i < 16; i++) {
            for (int j = 0; j < 16; j++) {
               var bgr = PaletteRun.FlipColorChannels(palettes[i][j]);
               model.WriteMultiByteValue(start + i * 32 + j * 2, 2, token, bgr);
            }
         }
      }

      public byte[][] ReadBlocks() {
         int blockCount = primaryBlocks;
         // each block is 16 bytes
         int start = ReadPointer(12);
         if (IsSecondary) blockCount = 1024 - blockCount;
         var attributeStart = ReadPointer(attributeOffset);
         EstimateBlockCount(ref blockCount, start, attributeStart);
         var data = new byte[blockCount][];
         for (int i = 0; i < blockCount; i++) {
            data[i] = new byte[16];
            for (int j = 0; j < data[i].Length; j++) {
               data[i][j] = model[start + i * data[i].Length + j];
            }
         }
         return data;
      }

      // TODO make it possible to expand blocks to their full size
      public void WriteBlocks(byte[][] blocks, ModelDelta token) {
         int start = ReadPointer(12);
         for (int i = 0; i < blocks.Length; i++) {
            for (int j = 0; j < blocks[i].Length; j++) {
               token.ChangeData(model, start + i * blocks[i].Length + j, blocks[i][j]);
            }
         }
      }

      public byte[][] ReadBlockAttributes() {
         int blockCount = primaryBlocks;
         int start = ReadPointer(12);
         if (IsSecondary) blockCount = 1024 - blockCount;
         var attributeStart = ReadPointer(attributeOffset);
         EstimateBlockCount(ref blockCount, start, attributeStart);
         var data = new byte[blockCount][];
         for (int i = 0; i < blockCount; i++) {
            data[i] = new byte[BytesPerAttribute];
            for (int j = 0; j < data[i].Length; j++) {
               data[i][j] = model[attributeStart + i * data[i].Length + j];
            }
         }
         return data;
      }

      // TODO make it possible to exand attributes to their full size
      public void WriteBlockAttributes(byte[][] attributes, ModelDelta token) {
         int attributeStart = ReadPointer(attributeOffset);
         for (int i = 0; i < attributes.Length; i++) {
            attributes[i] = new byte[BytesPerAttribute];
            for (int j = 0; j < attributes[i].Length; j++) {
               token.ChangeData(model, attributeStart + i * attributes[i].Length + j, attributes[i][j]);
            }
         }
      }

      public static IPixelViewModel RenderBlock(byte[] block, int[][,] tiles, short[][] palettes) {
         var canvas = new CanvasPixelViewModel(16, 16);

         // bottom layer
         var tile = Read(block, 0, tiles, palettes);
         canvas.Draw(tile, 0, 0);

         tile = Read(block, 1, tiles, palettes);
         canvas.Draw(tile, 8, 0);

         tile = Read(block, 2, tiles, palettes);
         canvas.Draw(tile, 0, 8);

         tile = Read(block, 3, tiles, palettes);
         canvas.Draw(tile, 8, 8);

         // top layer
         tile = Read(block, 4, tiles, palettes);
         canvas.Draw(tile, 0, 0);

         tile = Read(block, 5, tiles, palettes);
         canvas.Draw(tile, 8, 0);

         tile = Read(block, 6, tiles, palettes);
         canvas.Draw(tile, 0, 8);

         tile = Read(block, 7, tiles, palettes);
         canvas.Draw(tile, 8, 8);

         return canvas;
      }

      private void EstimateBlockCount(ref int blockCount, int blockStart, int attributeStart) => EstimateBlockCount(model, ref blockCount, blockStart, attributeStart);
      public static void EstimateBlockCount(IDataModel model, ref int blockCount, int blockStart, int attributeStart) {
         var blockLength = 0x10;
         IFormattedRun run;
         for (run = model.GetNextRun(blockStart + 1); run.Start < attributeStart; run = model.GetNextRun(run.Start + run.Length)) {
            if (run is NoInfoRun || run is PointerRun) continue;
            break;
         }
         blockCount = Math.Min(blockCount, (run.Start - blockStart) / blockLength);
         if (blockCount < 1) blockCount = 1;
      }

      private void EstimateTileCount(ref int tileCount, int tileStart) => EstimateTileCount(model, ref tileCount, tileStart);
      public static void EstimateTileCount(IDataModel model, ref int tileCount, int tileStart) {
         // each tile is 32 bytes
         int tileLength = 0x20;
         IFormattedRun run;
         for (run = model.GetNextRun(tileStart + 1); run.Start < tileStart + tileCount * tileLength; run = model.GetNextRun(run.Start + run.Length)) {
            if (run is PointerRun) continue;
            if (run is NoInfoRun) {
               if ((run.Start - tileStart) % 0x20 == 0) {
                  // might be a real anchor to unprocessed data, like another tileset
               } else {
                  continue;
               }
            }
            break;
         }
         tileCount = Math.Min(tileCount, (run.Start - tileStart) / tileLength);
         if (tileCount < 1) tileCount = 1;
      }

      public static IPixelViewModel Read(byte[] block, int index, int[][,] tiles, short[][] palettes) {
         var (pal, hFlip, vFlip, tile) = LzTilemapRun.ReadTileData(block, index, 2);

         if (pal >= palettes.Length) return new ReadonlyPixelViewModel(8, 8, new short[64]);

         if (tiles.Length < tile) {
            return new ReadonlyPixelViewModel(new SpriteFormat(4, 1, 1, default), new short[64]);
         }

         var tileData = new short[64];
         for (int yy = 0; yy < 8; yy++) {
            for (int xx = 0; xx < 8; xx++) {
               var inX = hFlip ? 7 - xx : xx;
               var inY = vFlip ? 7 - yy : yy;
               if (tiles.Length <= tile || tiles[tile] == null || palettes.Length <= pal || palettes[pal] == null) tileData[yy * 8 + xx] = 0;
               else tileData[yy * 8 + xx] = palettes[pal][tiles[tile][inX, inY]];
            }
         }
         return new ReadonlyPixelViewModel(8, 8, tileData, index < 4 ? (short)-1 : palettes[pal][0]);
      }
   }
}
