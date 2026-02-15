using System;

namespace SaperMultiplayer;

internal struct BoolArr8
{
    // Consts
    public static readonly BoolArr8 AllTrue = new BoolArr8(0xFF);


    // Variables
    private byte data;

    // Getters and Setters
    public byte RawData
    {
        get => data;
        set => data = value;
    }


    // Constructor
    public BoolArr8(byte initialValue = 0)
    {
        data = initialValue;
    }


    public bool this[int index]
    {
        get
        {
            if (index < 0 || index >= 8)
                throw new IndexOutOfRangeException("Index must be between 0 and 7.");

            return (data & (1 << index)) != 0;
        }
        set
        {
            if (index < 0 || index >= 8)
                throw new IndexOutOfRangeException("Index must be between 0 and 7.");

            if (value)
                data |= (byte)(1 << index);
            else
                data &= (byte)~(1 << index);
        }
    }

    public bool IsAllTrue => data == 0xFF;

    public bool IsAllFalse => data == 0x00;
}
