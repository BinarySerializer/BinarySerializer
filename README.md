# BinarySerializer
BinarySerializer is a library for reading/writing from/to binary files created by [RayCarrot](https://github.com/RayCarrot) and [Byvar](https://github.com/byvar). It was originally a part of [Ray1Map](https://github.com/Adsolution/Ray1Map) until it was [moved to this library](https://github.com/Adsolution/Ray1Map/commit/cade8b3926e41af9c0bd19c097e181745db20e64)

## Usage

### Context
In order to serialize data from/to a file or other source a `Context` needs to be created.
```cs
using (var context = new Context(basePath))
{

}
```
The base path passed in will be used as the base path for all files added to the context.

### Binary File
A `BinaryFile` needs to be added for each file source to use in the context.
```cs
context.AddFile(new LinearSerializedFile(context)
{
    FilePath = relativeFilePath
});
```
There are different `BinaryFile` types to use, such as `LinearSerializedFile`, `MemoryMappedFile` and `StreamFile`. Additional ones can be created by inheriting from `BinaryFile`.

### Serializable structs
For each file struct to serialize from/to you will need a matching class inheriting from `BinarySerializable`. This ensures the class can handle the data serialization.
```cs
public class FileStruct : BinarySerializable
{
    public int FirstValue { get; set; }
    public byte SecondValue { get; set; }

    public override void SerializeImpl(SerializerObject s)
    {
        FirstValue = s.Serialize<int>(FirstValue, name: nameof(FirstValue));
        SecondValue = s.Serialize<byte>(SecondValue, name: nameof(SecondValue));
    }
}
```

The `SerializerObject` has many helpful methods, such as `DoAt()` for following a pointer and serializing the data it points to.
```cs
DataPointer = s.SerializePointer(DataPointer, name: nameof(DataPointer));
Data = s.DoAt(DataPointer, () => s.Serialize<byte>(Data, name: nameof(Data)));
```

### Read/write
To read/write from/to files the `FileFactory` class can be used.
```cs
var fileStruct = FileFactory.Read<FileStruct>(relativeFilePath, context);
```
Make sure the path has been added to the context and that the generic type inherits from `BinarySerializable`!
