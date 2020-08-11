using NBTExplorer.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

namespace NBTUtil.Utils
{
    class ParsedSector : IEnumerable<Block>
    {
        private Vector3 sectorPosition;
        private DataNode sectionDataNode;
        private TagLongArrayDataNode blockStatesTag;
        private int[] blockStates = new int[4096];
        private int rawLongDataArrayLength;
        private Dictionary<int, string> blockPalettes;

        private List<Block> blocks = null;

        private int indexBitLength;

        public ParsedSector (Vector3 sectorPosition, DataNode sectionDataNode, TagLongArrayDataNode blockStatesTag, Dictionary<int, string> blockPalettes)
        {
            this.sectorPosition = sectorPosition;
            this.sectionDataNode = sectionDataNode;
            this.blockStatesTag = blockStatesTag;
            this.blockPalettes = blockPalettes;
            
            long[] rawBlockStates = blockStatesTag.Tag.ToTagLongArray();
            if (rawBlockStates == null)
                throw new ArgumentException("blockStatesTag's Tag is null");

            this.rawLongDataArrayLength = rawBlockStates.Length;

            indexBitLength = CalcIndexBitLength(rawBlockStates.Length);
            ParseBlockStates(new BitStream(rawBlockStates));
        }

        public IEnumerator<Block> GetEnumerator()
        {
            if (blocks == null)
                ParseBlocks();

            foreach (Block block in blocks)
                yield return block;
        }

        private void ParseBlocks()
        {
            blocks = new List<Block>();

            int blockIndex;
            string materialName;

            for (int i = 0; i < blockStates.Length; i++)
            {
                if ((blockIndex = blockStates[i]) == -1)
                    continue;

                blockPalettes.TryGetValue(blockIndex, out materialName);

                blocks.Add(new Block(this, SectorToBlock(sectorPosition, i % 4096), i % 4096, blockIndex, materialName));
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        private void ParseBlockStates(BitStream blockStateBitStream)
        {
            int[] parsedBlockStates = new int[4096];

            try
            {
                int longBitCounter = 0;
                for (int i = 0; i < parsedBlockStates.Length; i++, longBitCounter += indexBitLength)
                {
                    if (longBitCounter + indexBitLength >= 64)
                    {
                        for (int j = 0; j < 64 - longBitCounter; j++)
                        {
                            blockStateBitStream.IncrementBitOffset();
                        }

                        longBitCounter = 0;
                    }

                    parsedBlockStates[i] = (int)blockStateBitStream.ReadValue(indexBitLength);
                }
            }
            catch (InvalidOperationException)
            {
            }

            this.blockStates = parsedBlockStates;
        }

        public bool SetBlockData(Block block)
        {
            string material = block.material;

            int materialIndex = Utils.GetKeyByValue(blockPalettes, material, -1);

            if (materialIndex == -1)
                return false;

            blockStates[block.sectorIndex] = materialIndex;

            return true;
        }

        public bool SaveBlockStates()
        {    
            BitStream dataBitStream = new BitStream(new byte[rawLongDataArrayLength * 8]);

            for (int i = 0; i < blockStates.Length; i++)
            {
                if ((dataBitStream.totalOffset % 64) + indexBitLength - 1 >= 64)
                    dataBitStream.SkipThisLong();

                dataBitStream.WriteValue(blockStates[i], indexBitLength);
            }

            long[] data = dataBitStream.ToLongArray();
            string parsedData = "";
            for (int i = 0; i < data.Length; i++)
            {
                parsedData += data[i].ToString();

                if (i + 1 < data.Length)
                    parsedData += "|";
            }

            blockStatesTag.Parse(parsedData);

            return true;
        }

        private static int CalcIndexBitLength(int blockStateSize)
        {
            return blockStateSize >> 6;
        }
        private static Vector3 SectorToBlock(Vector3 sector, int index)
        {
            int x = index & 0xF;
            int y = (index >> 8) & 0xF;
            int z = (index >> 4) & 0xF;

            return new Vector3(sector.X + x, sector.Y + y, sector.Z + z);
        }
    }

    class Block
    {
        private ParsedSector blockIterator;
        public Vector3 blockPosition { get; }
        public int sectorIndex;

        private string _material;
        public int materialIndex { get; }
        public string material { 
            get { return _material;  }
            set { _material = value; SetMaterial(); }
        }

        public Block(ParsedSector blockIterator, Vector3 blockPosition, int sectorIndex, int materialIndex, string material)
        {
            this.blockIterator = blockIterator;
            this.blockPosition = blockPosition;
            this.sectorIndex = sectorIndex;
            this.materialIndex = materialIndex;
            this._material = material;
        }

        private void SetMaterial()
        {
            blockIterator.SetBlockData(this);
        }
    }
}
