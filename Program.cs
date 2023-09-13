using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Infinite_module_test.module_structs;
using static Infinite_module_test.code_utils;
using OodleSharp;
using System.Reflection.PortableExecutable;
using System.ComponentModel;

namespace Tag_Unpacker{
    internal class Program{
        static void Main(string[] args){
            // args should be as follows:
            // arg[0]: path of the module to unpack
            // arg[1]: unpacked tags directory, note that this should have the game version appended if we're doing that 
            // arg[2]: a directory to export csv files of common structures, for analysis
            // example args
            args = new string[]{
                "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\pc\\levels\\multi\\sgh_streets\\sgh_streets-rtx-new.module",
                "D:\\T\\PC\\sgh_streets-rtx-new\\", // include the "\\" at the end
                "D:\\T\\PC\\sgh_streets-rtx-new\\STRUCTS\\"
            };

            if (args.Length < 1){
                Console.WriteLine("No input module directory specified, failed to unpack");
                Console.ReadLine();
                return;}
            if (args.Length < 2){
                Console.WriteLine("No output tag directory specified, failed to unpack");
                Console.ReadLine();
                return;}
            string struct_dir = "";
            if (args.Length > 2){
                Console.WriteLine("Enabled struct dumping mode");
                struct_dir = args[2];}

            Console.WriteLine("begining unpack process");
            new unmodulatinator(args[0], args[1], struct_dir);
            Console.WriteLine("completed unpack process, press enter to exit");
            Console.ReadLine();
        }
    }
    public class unmodulatinator{
        public unmodulatinator(string _module_file_path, string _out_tag_directory, string _out_csv_directory){
            //module = new module_data();
            module_file_path = _module_file_path;
            out_tag_directory = _out_tag_directory;
            out_csv_directory = _out_csv_directory;
            new_unpack();
        }
        //module_data module;
        FileStream module_reader;
        string module_file_path;
        string out_tag_directory;
        string out_csv_directory;

        void new_unpack()
        {
            module new_module = new(module_file_path);
            foreach (var folder in new_module.file_groups){

                foreach (var tag in folder.Value){
                    try{
                        File.WriteAllBytes(out_tag_directory + folder.Key + "\\" + tag.name, new_module.get_tag_bytes(tag.source_file_header_index));
                    }catch (Exception ex) {
                        Console.WriteLine(tag.name + " could not be exported!: " + ex.Message);
                    }
                }
            }
        }
        /*
        void unpack(){

            // and then he said "it's module'n time"
            using (module_reader = new FileStream(module_file_path, FileMode.Open, FileAccess.Read)){
                // read module header
                module.module_info = read_and_convert_to<module_header>(module_header_size);

                // read module file headers
                module.files = new module_file[module.module_info.FileCount];
                for (int i = 0; i < module.files.Length; i++)
                    module.files[i] = read_and_convert_to<module_file>(module_file_size);

                // read the string table
                module.string_table = new byte[module.module_info.StringsSize];
                module_reader.Read(module.string_table, 0, module.module_info.StringsSize);

                // read the resource indicies?
                module.resource_table = new int[module.module_info.ResourceCount];
                for (int i = 0; i < module.resource_table.Length; i++)
                    module.resource_table[i] = read_and_convert_to<int>(4); // we should also fix this one too

                // read the data blocks
                module.blocks = new block_header[module.module_info.BlockCount];
                for (int i = 0; i < module.blocks.Length; i++)
                    module.blocks[i] = read_and_convert_to<block_header>(block_header_size);

                if (!string.IsNullOrEmpty(out_csv_directory)){
                    write_Filearray_csv(module.files, out_csv_directory + "tag_headers.csv");
                    //SaveArrayAsCSV(module.resource_table, out_csv_directory + "resources.csv");
                    //SaveArrayAsCSV(module.blocks, out_csv_directory + "data_blocks.csv");
                }

                // now to read the compressed data junk

                // align accordingly to 0x?????000 padding to read data
                long aligned_address = (module_reader.Position / 0x1000 + 1) * 0x1000;
                //module_reader.Seek(aligned_address, SeekOrigin.Begin);

                int nullindex = 0;

                for (int i = 0; i < module.files.Length; i++){
                    // read the flags to determine how to process this file
                    bool using_compression = (module.files[i].Flags & 0b00000001) > 0; // pretty sure this is true if reading_seperate_blocks is also true, confirmation needed
                    bool reading_separate_blocks = (module.files[i].Flags & 0b00000010) > 0;
                    bool reading_raw_file = (module.files[i].Flags & 0b00000100) > 0;

                    byte[] decompressed_data = new byte[module.files[i].TotalUncompressedSize];
                    long data_Address = aligned_address + module.files[i].get_dataoffset();

                    if (reading_separate_blocks){
                        for (int b = 0; b < module.files[i].BlockCount; b++){
                            var bloc = module.blocks[module.files[i].BlockIndex + b];
                            byte[] block_bytes;

                            if (bloc.Compressed == 1){
                                module_reader.Seek(data_Address + bloc.CompressedOffset, SeekOrigin.Begin);

                                byte[] bytes = new byte[bloc.CompressedSize];
                                module_reader.Read(bytes, 0, bytes.Length);
                                block_bytes = Oodle.Decompress(bytes, bytes.Length, bloc.UncompressedSize);
                            } else { // uncompressed
                                module_reader.Seek(data_Address + bloc.UncompressedOffset, SeekOrigin.Begin);

                                block_bytes = new byte[bloc.UncompressedSize];
                                module_reader.Read(block_bytes, 0, block_bytes.Length);
                            }
                            System.Buffer.BlockCopy(block_bytes, 0, decompressed_data, bloc.UncompressedOffset, block_bytes.Length);

                    }} else {  // is the manifest thingo, aka raw file, read data based off compressed and uncompressed length
                        module_reader.Seek(data_Address, SeekOrigin.Begin);
                        if (using_compression){
                            byte[] bytes = new byte[module.files[i].TotalCompressedSize];
                            module_reader.Read(bytes, 0, bytes.Length);
                            decompressed_data = Oodle.Decompress(bytes, bytes.Length, module.files[i].TotalUncompressedSize);
                        } else module_reader.Read(decompressed_data, 0, module.files[i].TotalUncompressedSize);
                    }
                    // umm thats enough for now, this tool does not need to process the tags anymore

                    // ok so now we need to unpack this tag, we'll basically just plop all of those decompressed bytes into a single file with the handy write all bytes function
                    // so first we need to know what file/folder to write to, so lets fetch the name
                    // NOTE: in later module versions, it does not provdei the file name, so we must either refere to a list of  names, or we must place the tag into a temp folder
                    // i guess we could aos attempt tp calculate which folder this tag belongs in, as we could see who references it and stikc it in the folder of the guy who references
                    // we could alos just group unknowns tags by tag group, so we'd ha ve the unknown folder, and then we'd have group folders inside that unknow folddedr that we'd then assort them into
                    // but realistically, we'll worry about that when we get around to it, or rather when 343 gets around to it

                    // get tag name
                    /*
                    string tag_name = "";
                    int byte_offset = 0;
                    while (true){
                        byte next_character = module.string_table[module.files[i].NameOffset + byte_offset];
                        if (next_character == 0) break;

                        tag_name += (char)next_character;
                        byte_offset++;
                    }
                    ///
                    // we are now going to sort tags into their respective group folder, and then unsigned hex tagID
                    string file_path = out_tag_directory;

                    if (module.files[i].ParentIndex != -1)
                    {
                        module_file par_tag = module.files[module.files[i].ParentIndex];
                        // get parent tag,, use their name
                        file_path += groupID_str(par_tag.ClassId) + "\\" + par_tag.GlobalTagId.ToString("X");
                        // figure out what index this resource is
                        int resource_index = -1;
                        for (int r = 0; r < par_tag.ResourceCount; r++){
                            if (module.resource_table[par_tag.ResourceIndex + r] == i){
                                resource_index = r;
                                break;
                            }
                        }
                        file_path += "_res_" + resource_index;
                        // determine whether is chunked or not??

                    }
                    else // regular file
                    {
                        // we need to remove the trailing spaces
                        if (module.files[i].GlobalTagId == -1) // then this is a special file
                            file_path += groupID_str(module.files[i].ClassId) + "\\" + module.files[i].GlobalTagId.ToString("X") + "_" + nullindex++;
                        else
                            file_path += groupID_str(module.files[i].ClassId) + "\\" + module.files[i].GlobalTagId.ToString("X");
                    }

                    // now write the file from the decompressed data (tag header + tag data + tag resource + whatever else we had)
                    Directory.CreateDirectory(Path.GetDirectoryName(file_path));
                    if (File.Exists(file_path))
                        Console.WriteLine("Warning: Overwriting file:" + file_path);
                    File.WriteAllBytes(file_path, decompressed_data);

                }
                // ok thats all, the tags have been read
        }}

        string groupID_str(int groupid){
            string result = "";
            result += (char)((groupid >> 24) & 0xFF);
            result += (char)((groupid >> 16) & 0xFF);
            result += (char)((groupid >> 8) & 0xFF);
            result += (char)(groupid & 0xFF);
            return result.Trim().Replace('*', '_');
        }
        void write_Filearray_csv(module_file[] content, string fileName){
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            using (StreamWriter file = new StreamWriter(fileName)){
                file.WriteLine("ClassGroup,Flags,BlockCount,BlockIndex,ResourceIndex,ClassId,DataOffset,Unk_0x14,TotalCompressedSize,TotalUncompressedSize,GlobalTagId,UncompressedHeaderSize,UncompressedTagDataSize,UncompressedResourceDataSize,UncompressedActualResourceDataSize,HeaderAlignment,TagDataAlightment,ResourceDataAligment,ActualResourceDataAligment,NameOffset,ParentIndex,AssetChecksum,AssetId,ResourceCount,Unk_0x54");
                for (int i = 0; i < module.files.Length; i++){
                    var cfile = module.files[i];
                    StringBuilder stringBuilder = new StringBuilder();

                    string row = string.Join(',', new object[] {
                        cfile.ClassGroup,
                        cfile.Flags,
                        cfile.BlockCount,
                        cfile.BlockIndex,
                        cfile.ResourceIndex,
                        groupID_str(cfile.ClassId),
                        cfile.get_dataoffset(),
                        cfile.get_dataflags(),
                        cfile.TotalCompressedSize,
                        cfile.TotalUncompressedSize,
                        cfile.GlobalTagId.ToString("X"),
                        cfile.UncompressedHeaderSize,
                        cfile.UncompressedTagDataSize,
                        cfile.UncompressedResourceDataSize,
                        cfile.UncompressedActualResourceDataSize,
                        cfile.HeaderAlignment,
                        cfile.TagDataAlightment,
                        cfile.ResourceDataAligment,
                        cfile.ActualResourceDataAligment,
                        cfile.NameOffset,
                        cfile.ParentIndex,
                        cfile.AssetChecksum.ToString("X"),
                        cfile.AssetId.ToString("X"),
                        cfile.ResourceCount,
                        cfile.Unk_0x54});
                    file.WriteLine(row);
                }
            }
        }

        T read_and_convert_to<T>(int read_length){
            byte[] bytes = new byte[read_length];
            module_reader.Read(bytes, 0, read_length);
            return KindaSafe_SuperCast<T>(bytes);
        }
*/
    }
}
