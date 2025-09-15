# Code Combiner for Unity

A lightweight toolkit that allows Unity code folders to be combined

## Installation

Unity Editor → Window ▸ Package Manager<br>
➕ Add package from Git URL<br>
https://github.com/Braneloc/CodeCombiner.git

_Unity downloads the package and recompiles scripts automatically._

## Features

- Combines all code files in a folder into one file
- Seperates Editor, Code and Test files
- Creates a .json type index
- Optionally recurse through any subfolders
- Zips the large created file ready for upload

## Problems this project solves

- Instead of uploading large amounts of code files to a LLM, you can upload a combined code file instead reducing file requirements on certain LLMs

## Usage

- From the tools menu
- Tools ▸ Code ▸ Combine C# Files
- Drag and drop a Unity code folder from the Project window.
- Combine and save

## Files
- The files are stored in the root of your _project_ folder, under _combined-code_ next to the Assets folder.<br>
I hope that made sense.

## Party on dudes  
![](https://avatars.githubusercontent.com/u/9757397?s=96&v=4)
