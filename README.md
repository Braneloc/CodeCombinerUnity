# Code Combiner for Unity

A lightweight toolkit that allows Unity code folders to be combined

## Installation

Unity Editor → Window ▸ Package Manager<br>
➕ Add package from Git URL<br>
https://github.com/Braneloc/CodeCombinerUnity.git

_Unity downloads the package and recompiles scripts automatically._

## Features

- Combines all code files in a folder into one **-main.cs** file <br>_(Recommended for upload)_
- Seperates Editor, Code and Test files
- Creates a .json file index
- Creates a .json list of classes
- Optionally recurse through any subfolders
- Zips the large created file ready for upload <br>_(Only upload if the **-main.cs** is very large)_

## Problems this project solves
- Instead of uploading large amounts of small code files for review, you can upload a combined code file instead reducing file requirements or cognative load.
- It is easier to upload a whole folder as a combined code file.

## Usage
- From the tools menu
- Tools ▸ Code ▸ Combine C# Files
- Drag and drop a Unity code folder from the Project window.
- Click Combine and save

## Files
- The files are stored in the root of your _project_ folder, under _combined-code_ next to the Assets folder.<br>

```
Project Root
  Assets
    FolderWithScripts
      script1.cs
      script2.cs
  combined-code
    FolderWithScripts-main.cs
    FolderWithScripts-main.json
    FolderWithScripts-types.json
    FolderWithScripts-Upload.zip

```
## Notes
- Disclaimer: Parts of this project were co-created by an AI LLM.

## Party on dudes  
![](https://avatars.githubusercontent.com/u/9757397?s=96&v=4)
