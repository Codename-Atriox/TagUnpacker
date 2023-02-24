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

namespace Tag_Unpacker{
    internal class Program{
        static void Main(string[] args){
            // args should be as follows:
            // arg[0]: path of the module to unpack
            // arg[1]: unpacked tags directory, note that this should have the game version appended if we're doing that 
            // arg[2]: a comma separated list of tag types to include/disclude - NOT IMPLEMENTED YET
            // example args
            args = new string[]{
                "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\pc\\levels\\multi\\ctf_bazaar\\ctf_bazaar-rtx-new.module",
                "D:\\T\\" // include the "\\" at the end
            };

            if (args.Length < 1){
                Console.WriteLine("No input module directory specified, failed to unpack");
                return;}
            if (args.Length < 2){
                Console.WriteLine("No output tag directory specified, failed to unpack");
                return;}

            Console.WriteLine("begining unpack process");
            new unmodulatinator(args[0], args[1]);
            Console.WriteLine("completed unpack process");
        }
    }
    public class unmodulatinator{
        public unmodulatinator(string _module_file_path, string _out_tag_directory){
            //module = new module_data();
            module_file_path = _module_file_path;
            out_tag_directory = _out_tag_directory;
            unpack();
        }
        module_data module;
        FileStream module_reader;
        string module_file_path;
        string out_tag_directory;

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


                // now to read the compressed data junk

                // align accordingly to 0x?????000 padding to read data
                long aligned_address = (module_reader.Position / 0x1000 + 1) * 0x1000;
                //module_reader.Seek(aligned_address, SeekOrigin.Begin);
                

                for (int i = 0; i < module.files.Length; i++){
                    // read the flags to determine how to process this file
                    bool using_compression = (module.files[i].Flags & 0b00000001) > 0; // pretty sure this is true if reading_seperate_blocks is also true, confirmation needed
                    bool reading_separate_blocks = (module.files[i].Flags & 0b00000010) > 0;
                    bool reading_raw_file = (module.files[i].Flags & 0b00000100) > 0;

                    byte[] decompressed_data = new byte[module.files[i].TotalUncompressedSize];
                    long data_Address = aligned_address + module.files[i].DataOffset;

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
                    string tag_name = "";
                    int byte_offset = 0;
                    while (true){
                        byte next_character = module.string_table[module.files[i].NameOffset + byte_offset];
                        if (next_character == 0) break;

                        tag_name += (char)next_character;
                        byte_offset++;
                    }
                    // fixup the name if needed
                    string file_path = out_tag_directory + tag_name;
                    string test_extension = Path.GetExtension(file_path);
                    if (test_extension.Contains(":"))
                        file_path = Path.ChangeExtension(file_path, test_extension.Replace(":", "-"));

                    // now write the file from the decompressed data (tag header + tag data + tag resource + whatever else we had)
                    Directory.CreateDirectory(Path.GetDirectoryName(file_path));
                    File.WriteAllBytes(file_path, decompressed_data);

                }
                // ok thats all, the tags have been read
        }}

        T read_and_convert_to<T>(int read_length){
            byte[] bytes = new byte[read_length];
            module_reader.Read(bytes, 0, read_length);
            return KindaSafe_SuperCast<T>(bytes);
        }
    }
}
