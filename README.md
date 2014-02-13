# TextQL .Net

This is a .Net version of the tiny but great tool [textql](https://github.com/dinedal/textql)

It allows you to easily execute SQL against delimited files like CSV TSV etc..

## Requirements

This project runs with .Net 4 Framework 

###Compatibility:

- Microsoft Windows XP (but not Starter, Media Center or Tablet editions)
- Microsoft Windows Server 2003
- Windows Vista
- Windows 7 (included with OS)
- Windows 8 +
- Windows Server 2008

## Usage
Usage defers a little

```bash
  --console or -c =false: After all commands are run, open sqlite3 console with this data
  --dlm or -d=",": Delimiter between fields =tab for tab, =0x## to specify a character code in hex
  --header or -h=false: Treat file as having the first row as a header row
  --save-to="": If set, sqlite3 db is left on disk at this path
  --source or -s="stdin": Source file to load
  --sql or -q="": SQL Command(s) to run on the data
  --table-name or -t="tbl": Override the default table name (tbl)
  --verbose or -v=false: Enable verbose logging
```

Example session taken on the original repo [textql for go language](https://github.com/dinedal/textql)

![textql_usage_session](https://raw.github.com/dinedal/textql/master/textql_usage.gif)


```

## License

New MIT License - Copyright (c) 2014, Joan Caron

See LICENSE for details