using System;
using System.Collections.Generic;
using NBTExplorer.Model;
using System.Numerics;
using System.Text.RegularExpressions;
using NBTUtil.Utils;
using System.Text;

namespace NBTUtil.Ops
{
    class BlockFinderOperation : ConsoleOperation
    {
        private static Regex mcaRegex = new Regex("r\\.-?\\d+\\.-?\\d+\\.mca");
        private static Regex chunkRegex = new Regex("-?\\d+\\.-?\\d+");
        private static Regex sectorRegex = new Regex("Y:-?\\d+");

        private string jsonPathTemplate = "exportedBlocks";

        private string materialName = null;
        private string actionName = "json";
        private string jsonPath;

        private int _executions;
        private int executions { get { return _executions; } set { _executions = value; jsonPath = jsonPathTemplate + executions + ".json"; } }

        public BlockFinderOperation()
        {
            executions = 0;
        }

        public override bool OptionsValid(ConsoleOptions options)
        {
            ParseOptions(options.Values);

            if (actionName.Equals("delete"))
            {
                Console.Out.WriteLine("WARNING: Deletion will result in air-duplicates in Palette! Lighting will not be updated!");
            }

            if (materialName == null)
            {
                Console.Out.WriteLine("You have to specify a material to look for!");
                return false;
            }

            return true;
        }

        public override bool CanProcess(DataNode dataNode)
        {
            if (!(dataNode is DirectoryDataNode))
            {
                Console.Out.WriteLine("Selected File (" + dataNode.NodePath + ") must be a folder!");
                return false;
            }

            Console.Out.WriteLine("Target World: " + dataNode.NodeDisplay);
            Console.Out.WriteLine("Target Material: " + materialName);
            Console.Out.WriteLine("Action: " + actionName);
            
            if (actionName.Equals("json"))
            {
                Console.Out.WriteLine("Json Path: " + jsonPath);
            }

            Console.Out.WriteLine();

            dataNode.Expand();

            if (FindRegionFolder((DirectoryDataNode)dataNode) == null)
            {
                Console.Out.WriteLine("Couldn't find region folder in the world-directory!\nCheck if the path is valid ('" + dataNode.NodePath + ")");
                return false;
            }

            dataNode.Collapse();

            return true;
        }

        /** Process World **/
        public override bool Process(DataNode dataNode, ConsoleOptions options)
        {
            List<Vector3> foundBlocks = null;

            string searchMaterial = options.Values[0].Replace("material=", "");

            if (actionName.Equals("json"))
                foundBlocks = new List<Vector3>();

            dataNode.Expand();

            DirectoryDataNode regionDirectory;
            if (!(dataNode is DirectoryDataNode) || (regionDirectory = FindRegionFolder((DirectoryDataNode)dataNode)) == null)
            {
                dataNode.Collapse();
                return false;
            }

            regionDirectory.Expand();

            int toProcessFiles = regionDirectory.Nodes.Count;
            int processedFiles = 0;

            bool success = true;
            Vector2 regionPosition;
            foreach (DataNode regionNode in regionDirectory.Nodes)
            {
                WriteProgressBar(processedFiles, toProcessFiles);

                if (regionNode is RegionFileDataNode regionFileDataNode)
                {
                    regionPosition = WorldToRegion(regionNode.NodeDisplay);

                    regionFileDataNode.Expand();

                    success = !ProcessRegionFile(regionFileDataNode, regionPosition, searchMaterial, actionName, foundBlocks) ? false : success;

                    regionFileDataNode.Collapse();
                }

                processedFiles++;
            }

            WriteProgressBar(processedFiles, toProcessFiles);

            if (foundBlocks == null)
            {
                regionDirectory.Save();
            }
            else
            {
                Console.Out.WriteLine("Found " + foundBlocks.Count + " blocks!");
                Console.Out.Write("Writing to Json-File...");

                JsonUtils.saveListAsJsonFile(foundBlocks, jsonPath);

                Console.Out.WriteLine("DONE!");
            }

            regionDirectory.Collapse();

            executions++;

            return success;
        }

        /** Process Region **/
        private static bool ProcessRegionFile(RegionFileDataNode regionFileNode, Vector2 regionPosition, string searchMaterial, string actionType, List<Vector3> foundBlocks)
        {
            bool success = true;
            Vector2 chunkPosition;
            foreach (DataNode dataNode in regionFileNode.Nodes)
            {
                if (!(dataNode is RegionChunkDataNode regionChunkNode))
                    continue;

                chunkPosition = RegionToChunk(regionPosition, regionChunkNode.NodePathName);

                regionChunkNode.Expand();

                success = !ProcessChunkNode((RegionChunkDataNode)dataNode, chunkPosition, searchMaterial, actionType, foundBlocks) ? false : success;

                regionChunkNode.Collapse();
            }

            return success;
        }

        /** Process Chunk **/
        private static bool ProcessChunkNode(RegionChunkDataNode chunkDataNode, Vector2 chunkPosition, string searchMaterial, string actionType, List<Vector3> foundBlocks)
        {
            DataNode levelDataNode = GetDataNodeByName(chunkDataNode.Nodes, "Level");

            if (levelDataNode == null)
                return true;

            levelDataNode.Expand();

            DataNode sectionsDataNode = GetDataNodeByName(levelDataNode.Nodes, "Sections");
            if (sectionsDataNode == null)
            {
                levelDataNode.Collapse();
                return false;
            }

            sectionsDataNode.Expand();

            Dictionary<int, string> paletteBlocks;

            DataNode paletteDataNode;
            DataNode sectorYNode;
            DataNode sectorBlockStates;
            bool success = true;
            Vector3 sectorPosition;
            foreach (DataNode sectionDataNode in sectionsDataNode.Nodes)
            {
                sectionDataNode.Expand();

                sectorYNode = GetDataNodeByName(sectionDataNode.Nodes, "Y");
                sectorPosition = ChunkToSector(chunkPosition, sectorYNode.NodeDisplay);
                sectorBlockStates = GetDataNodeByName(sectionDataNode.Nodes, "BlockStates");
                paletteBlocks = new Dictionary<int, string>();

                if (sectorPosition.Y < 0)
                    continue;

                paletteDataNode = GetDataNodeByName(sectionDataNode.Nodes, "Palette");
                if (paletteDataNode != null && paletteDataNode is TagListDataNode sectionsListNode)
                {
                    sectionsListNode.Expand();

                    success = !ProcessPalette(sectionsListNode, searchMaterial, actionType, paletteBlocks) ? false : success;

                    sectionsListNode.Collapse();
                }

                if (paletteBlocks.Count == 0 || sectorBlockStates == null || !(sectorBlockStates is TagLongArrayDataNode longArrayDataNode))
                    continue;

                ParsedSector blockIterator = new ParsedSector(sectorPosition, sectionDataNode, longArrayDataNode, paletteBlocks);

                foreach (Block block in blockIterator)
                {
                    if (block.material != null && block.material.Equals(searchMaterial))
                    {
                        foundBlocks.Add(block.blockPosition);
                        // Console.Out.WriteLine("Block " + block.material + " at " + block.blockPosition);
                    }
                }

                // blockIterator.SaveBlockStates();
            }

            return true;
        }

        /** Process Palette **/
        private static bool ProcessPalette(TagListDataNode sectionsListNode, string searchMaterial, string actionType, Dictionary<int, string> palettes)
        {
            DataNode nameEntry;
            Int32 counter = 0;
            foreach (DataNode paletteEntry in sectionsListNode.Nodes)
            {
                paletteEntry.Expand();

                nameEntry = GetDataNodeByName(paletteEntry.Nodes, "Name");

                if (nameEntry != null && nameEntry is TagStringDataNode materialName)
                {
                    if (actionType.Equals("json"))
                        palettes.Add(counter, materialName.Tag.ToString());

                    if (materialName.Tag.ToString().Equals(searchMaterial))
                        if (actionType.Equals("delete"))
                            materialName.Parse("minecraft:air");
                }

                paletteEntry.Collapse();
                counter++;
            }

            return true;
        }

        /****************************************************/
        /*                     Utils                        */
        /****************************************************/


        /** Get Region Folder **/
        private static DirectoryDataNode FindRegionFolder(DirectoryDataNode worldFolder)
        {
            DirectoryDataNode regionDirectoryNode = null;

            DataNode regionDataNode = GetDataNodeByName(worldFolder.Nodes, "region");
            if (regionDataNode != null && regionDataNode is DirectoryDataNode)
                regionDirectoryNode = (DirectoryDataNode) regionDataNode;

            return regionDirectoryNode;
        }

        /** Get Node by Name **/
        public static DataNode GetDataNodeByName(DataNodeCollection dataNodeCollection, string dataNodeName)
        {
            foreach (DataNode dataNode in dataNodeCollection)
                if (dataNode.NodeDisplay.Equals(dataNodeName) || dataNode.NodeName.Equals(dataNodeName))
                    return dataNode;

            return null;
        }

        private void ParseOptions(List<string> optionsList)
        {
            string command;
            string value;
            foreach (string option in optionsList)
            {
                command = option.Split('=')[0];
                value = option.Substring(command.Length + 1);

                switch (command)
                {
                    case "material":
                        materialName = value;
                        break;

                    case "action":
                        actionName = value;
                        break;

                    case "json_path":
                        jsonPathTemplate = value;
                        break;
                }
            }
        }

        private static void WriteProgressBar(int done, int amount)
        {
            int dots = 20;
            int percent = (done != 0) ? (int)((float)done / amount * 100) : 0;

            Console.CursorLeft = 0;

            StringBuilder progressBar = new StringBuilder();
            progressBar.Append(percent).Append('%');

            if (percent < 10)
                progressBar.Append(' ');

            progressBar.Append(" [");

            for (int i = 0; i < dots; i++)
            {
                if (i * (100 / dots) < percent)
                {
                    progressBar.Append('*');
                } else
                {
                    progressBar.Append('.');
                }
            }

            progressBar.Append(']');

            Console.Write(progressBar);

            if (percent == 100)
                Console.WriteLine();
        }

        private static Vector2 WorldToRegion(string displayName)
        {
            if (mcaRegex.Matches(displayName) == null)
                return new Vector2(0, 0);

            string[] parts = displayName.Split('.');

            return new Vector2(Int32.Parse(parts[1]) * 512, Int32.Parse(parts[2]) * 512);
        }

        private static Vector2 RegionToChunk(Vector2 region, string nodePathName)
        {
            if (chunkRegex.Matches(nodePathName) == null)
                return new Vector2(0, 0);

            string[] parts = nodePathName.Split('.');

            return new Vector2(region.X + Int32.Parse(parts[0]) * 16, region.Y + Int32.Parse(parts[1]) * 16);
        }

        private static Vector3 ChunkToSector(Vector2 chunk, string displayName)
        {
            if (sectorRegex.Matches(displayName) == null)
                return new Vector3(0, 0, 0);

            string[] parts = displayName.Split(':');

            return new Vector3(chunk.X, Int32.Parse(parts[1]) * 16, chunk.Y);
        }
    }
}
