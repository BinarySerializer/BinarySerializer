﻿#nullable enable
using System;
using System.Text;

namespace BinarySerializer
{
    public static class PointerFunctions
    {
        public delegate T? SerializeFunction<T>(SerializerObject s, T? value, string? name = null);

        public static SerializeFunction<T> SerializeObject<T>(Action<T>? onPreSerialize = null) 
            where T : BinarySerializable, new() 
        {
            return (s, value, name) => s.SerializeObject<T>(value, onPreSerialize: onPreSerialize, name: name);
        }

        public static SerializeFunction<T[]> SerializeObjectArray<T>(long count, Action<T, int>? onPreSerialize = null) 
            where T : BinarySerializable, new() 
        {
            return (s, value, name) => s.SerializeObjectArray<T>(value, count, onPreSerialize: onPreSerialize, name: name);
        }

        public static SerializeFunction<T[]> SerializeObjectArrayUntil<T>(
            Func<T, bool> conditionCheckFunc, 
            Func<T>? getLastObjFunc = null, 
            Action<T, int>? onPreSerialize = null) 
            where T : BinarySerializable, new() 
        {
            return (s, value, name) => s.SerializeObjectArrayUntil<T>(
                obj: value, 
                conditionCheckFunc: conditionCheckFunc, 
                getLastObjFunc: getLastObjFunc, 
                onPreSerialize: onPreSerialize, 
                name: name);
        }

        public static SerializeFunction<T> Serialize<T>() =>
            (s, value, name) => s.Serialize<T>(value, name: name);

        public static SerializeFunction<T[]> SerializeArray<T>(long count) =>
            (s, value, name) => s.SerializeArray<T>(value, count, name: name);

        public static SerializeFunction<T[]> SerializeArrayUntil<T>(Func<T, bool> conditionCheckFunc, Func<T>? getLastObjFunc = null) =>
            (s, value, name) => s.SerializeArrayUntil<T>(value, conditionCheckFunc, getLastObjFunc: getLastObjFunc, name: name);

        public static SerializeFunction<string> SerializeString(long? length = null, Encoding? encoding = null) =>
            (s, value, name) => s.SerializeString(value, length: length, encoding: encoding, name: name);

        public static SerializeFunction<Pointer> SerializePointer(
            PointerSize size = PointerSize.Pointer32, 
            Pointer? anchor = null, 
            bool allowInvalid = false, 
            long? nullValue = null) 
        {
            return (s, value, name) => s.SerializePointer(
                obj: value, 
                size: size, 
                anchor: anchor, 
                allowInvalid: allowInvalid, 
                nullValue: nullValue, 
                name: name);
        }

        public static SerializeFunction<Pointer<T>> SerializePointer<T>(
            PointerSize size = PointerSize.Pointer32, 
            Pointer? anchor = null, 
            bool allowInvalid = false, 
            long? nullValue = null) 
        {
            return (s, value, name) => s.SerializePointer<T>(
                obj: value, 
                size: size, 
                anchor: anchor, 
                allowInvalid: allowInvalid, 
                nullValue: nullValue, 
                name: name);
        }

        public static SerializeFunction<Pointer?[]> SerializePointerArray(
            long count, 
            PointerSize size = PointerSize.Pointer32, 
            Pointer? anchor = null, 
            bool allowInvalid = false, 
            long? nullValue = null) 
        {
            return (s, value, name) => s.SerializePointerArray(
                obj: value, 
                count: count, 
                size: size, 
                anchor: anchor, 
                allowInvalid: allowInvalid, 
                nullValue: nullValue, 
                name: name);
        }
        public static SerializeFunction<Pointer<T>[]> SerializePointerArray<T>(
            long count, 
            PointerSize size = PointerSize.Pointer32, 
            Pointer? anchor = null, 
            bool allowInvalid = false, 
            long? nullValue = null)
        {
            return (s, value, name) => s.SerializePointerArray<T>(
                obj: value, 
                count: count, 
                size: size, 
                anchor: anchor, 
                allowInvalid: allowInvalid, 
                nullValue: nullValue, 
                name: name);
        }
    }
}