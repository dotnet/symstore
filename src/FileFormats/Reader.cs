﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.


namespace FileFormats
{
    public class Reader
    {
        public Reader(IAddressSpace dataSource, bool isBigEndian = false) :
            this(dataSource, new LayoutManager().AddPrimitives(isBigEndian).AddEnumTypes().AddTStructTypes())
        { }

        public Reader(IAddressSpace dataSource, LayoutManager layoutManager)
        {
            DataSource = dataSource;
            LayoutManager = layoutManager;
        }

        public LayoutManager LayoutManager { get; private set; }
        public IAddressSpace DataSource { get; private set; }


        public T[] ReadArray<T>(ulong position, uint elementCount)
        {
            return (T[])LayoutManager.GetArrayLayout<T[]>(elementCount).Read(DataSource, position);
        }

        public T[] ReadArray<T>(ref ulong position, uint elementCount)
        {
            uint bytesRead;
            T[] ret = (T[])LayoutManager.GetArrayLayout<T[]>(elementCount).Read(DataSource, position, out bytesRead);
            position += bytesRead;
            return ret;
        }

        public T Read<T>(ulong position)
        {
            return (T)LayoutManager.GetLayout<T>().Read(DataSource, position);
        }

        public T Read<T>(ref ulong position)
        {
            uint bytesRead;
            T ret = (T)LayoutManager.GetLayout<T>().Read(DataSource, position, out bytesRead);
            position += bytesRead;
            return ret;
        }

        public uint Read(ulong position, byte[] buffer, uint bufferOffset, uint count)
        {
            return DataSource.Read(position, buffer, bufferOffset, count);
        }

        public byte[] Read(ulong position, uint count)
        {
            return DataSource.Read(position, count);
        }

        public byte[] Read(ref ulong position, uint count)
        {
            byte[] ret = DataSource.Read(position, count);
            position += count;
            return ret;
        }

        public ulong Length { get { return DataSource.Length; } }

        public uint SizeOf<T>()
        {
            return LayoutManager.GetLayout<T>().Size;
        }

        public Reader WithRelativeAddressSpace(ulong startOffset, ulong length)
        {
            return WithAddressSpace(new RelativeAddressSpace(DataSource, startOffset, length));
        }

        public Reader WithRelativeAddressSpace(ulong startOffset, ulong length, long baseToRelativeShift)
        {
            return WithAddressSpace(new RelativeAddressSpace(DataSource, startOffset, length, baseToRelativeShift));
        }

        public Reader WithAddressSpace(IAddressSpace addressSpace)
        {
            return new Reader(addressSpace, LayoutManager);
        }
    }
}
