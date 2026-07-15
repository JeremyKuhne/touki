// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

internal static class BinaryFormattedObjectFixtures
{
    internal const string Int32 = """
        AAEAAAD/////AQAAAAAAAAAEAQAAAAxTeXN0ZW0uSW50MzIBAAAAB21fdmFsdWUACCoAAAAL
        """;

    internal const string ListInt32 = """
        AAEAAAD/////AQAAAAAAAAAEAQAAAH5TeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5MaXN0YDFbW1N5c3RlbS5JbnQzMiwg
        bXNjb3JsaWIsIFZlcnNpb249NC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRl
        MDg5XV0DAAAABl9pdGVtcwVfc2l6ZQhfdmVyc2lvbgcAAAgICAkCAAAABQAAAAUAAAAPAgAAAAgAAAAIAQAAAAIAAAADAAAA
        BQAAAAgAAAAAAAAAAAAAAAAAAAAL
        """;

    internal const string DateTime = """
        AAEAAAD/////AQAAAAAAAAAEAQAAAA9TeXN0ZW0uRGF0ZVRpbWUCAAAABXRpY2tzCGRhdGVEYXRhAAAJEAAY3wgJrN0IABjf
        CAms3UgL
        """;

    internal const string RegisteredPayload = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAIVRvdWtpLlJlc291cmNlcy5SZWdpc3RlcmVkUGF5bG9hZAMAAAAETmFtZQZO
        dW1iZXIETmV4dAEABAghVG91a2kuUmVzb3VyY2VzLlJlZ2lzdGVyZWRQYXlsb2FkAgAAAAIAAAAGAwAAAARyb290KgAAAAoL
        """;

    internal const string RegisteredPayloadCycle = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAIVRvdWtpLlJlc291cmNlcy5SZWdpc3RlcmVkUGF5bG9hZAMAAAAETmFtZQZO
        dW1iZXIETmV4dAEABAghVG91a2kuUmVzb3VyY2VzLlJlZ2lzdGVyZWRQYXlsb2FkAgAAAAIAAAAGAwAAAAVjeWNsZQcAAAAJ
        AQAAAAs=
        """;

    internal const string RegisteredPayloadArray = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAcBAAAAAAEAAAACAAAABCFUb3VraS5SZXNvdXJjZXMuUmVnaXN0ZXJlZFBheWxvYWQC
        AAAACQMAAAAJBAAAAAUDAAAAIVRvdWtpLlJlc291cmNlcy5SZWdpc3RlcmVkUGF5bG9hZAMAAAAETmFtZQZOdW1iZXIETmV4
        dAEABAghVG91a2kuUmVzb3VyY2VzLlJlZ2lzdGVyZWRQYXlsb2FkAgAAAAIAAAAGBQAAAAVmaXJzdAEAAAAKAQQAAAADAAAA
        BgYAAAAGc2Vjb25kAgAAAAoL
        """;

    internal const string CallbackPayload = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAH1RvdWtpLlJlc291cmNlcy5DYWxsYmFja1BheWxvYWQBAAAABVZhbHVlAQIA
        AAAGAwAAAAhjYWxsYmFjaws=
        """;

    internal const string SerializablePayload = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAI1RvdWtpLlJlc291cmNlcy5TZXJpYWxpemFibGVQYXlsb2FkAQAAAAVWYWx1
        ZQECAAAABgMAAAASc2VyaWFsaXphdGlvbi1pbmZvCw==
        """;

    internal const string IntPtr = """
        AAEAAAD/////AQAAAAAAAAAEAQAAAA1TeXN0ZW0uSW50UHRyAQAAAAV2YWx1ZQAJKgAAAAAAAAAL
        """;

    internal const string UIntPtr = """
        AAEAAAD/////AQAAAAAAAAAEAQAAAA5TeXN0ZW0uVUludFB0cgEAAAAFdmFsdWUAECoAAAAAAAAACw==
        """;

    internal const string NotSupportedException = """
        AAEAAAD/////AQAAAAAAAAAEAQAAABxTeXN0ZW0uTm90U3VwcG9ydGVkRXhjZXB0aW9uDAAAAAlDbGFzc05hbWUHTWVzc2Fn
        ZQREYXRhDklubmVyRXhjZXB0aW9uB0hlbHBVUkwQU3RhY2tUcmFjZVN0cmluZxZSZW1vdGVTdGFja1RyYWNlU3RyaW5nEFJl
        bW90ZVN0YWNrSW5kZXgPRXhjZXB0aW9uTWV0aG9kB0hSZXN1bHQGU291cmNlDVdhdHNvbkJ1Y2tldHMBAQMDAQEBAAEAAQce
        U3lzdGVtLkNvbGxlY3Rpb25zLklEaWN0aW9uYXJ5EFN5c3RlbS5FeGNlcHRpb24ICAIGAgAAABxTeXN0ZW0uTm90U3VwcG9y
        dGVkRXhjZXB0aW9uBgMAAAANbm90IHN1cHBvcnRlZAoKCgoKAAAAAAoVFROACgoL
        """;

    internal const string Decimal = """
        AAEAAAD/////AQAAAAAAAAAEAQAAAA5TeXN0ZW0uRGVjaW1hbAQAAAAFZmxhZ3MCaGkCbG8DbWlkAAAAAAgICAgAAAQAAAAA
        ABXNWwcAAAAACw==
        """;

    internal const string TimeSpan = """
        AAEAAAD/////AQAAAAAAAAAEAQAAAA9TeXN0ZW0uVGltZVNwYW4BAAAABl90aWNrcwAJAJymkgwAAAAL
        """;

    internal const string Int32Array = """
        AAEAAAD/////AQAAAAAAAAAPAQAAAAQAAAAIAgAAAAMAAAAFAAAABwAAAAs=
        """;

    internal const string StringArray = """
        AAEAAAD/////AQAAAAAAAAARAQAAAAMAAAAGAgAAAAVmaXJzdAoGAwAAAAV0aGlyZAs=
        """;

    internal const string SharedReferencePayload = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAJlRvdWtpLlJlc291cmNlcy5TaGFyZWRSZWZlcmVuY2VQYXlsb2FkAgAAAAVG
        aXJzdAZTZWNvbmQEBCFUb3VraS5SZXNvdXJjZXMuUmVnaXN0ZXJlZFBheWxvYWQCAAAAIVRvdWtpLlJlc291cmNlcy5SZWdp
        c3RlcmVkUGF5bG9hZAIAAAACAAAACQMAAAAJAwAAAAUDAAAAIVRvdWtpLlJlc291cmNlcy5SZWdpc3RlcmVkUGF5bG9hZAMA
        AAAETmFtZQZOdW1iZXIETmV4dAEABAghVG91a2kuUmVzb3VyY2VzLlJlZ2lzdGVyZWRQYXlsb2FkAgAAAAIAAAAGBAAAAAZz
        aGFyZWQRAAAACgs=
        """;

    internal const string SerializableCycle = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAIVRvdWtpLlJlc291cmNlcy5TZXJpYWxpemFibGVDeWNsZQIAAAAFVmFsdWUE
        TmV4dAAECCFUb3VraS5SZXNvdXJjZXMuU2VyaWFsaXphYmxlQ3ljbGUCAAAAAgAAACoAAAAJAQAAAAs=
        """;

    internal const string NodeWithNodeStruct = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAIlRvdWtpLlJlc291cmNlcy5Ob2RlV2l0aE5vZGVTdHJ1Y3QCAAAABVZhbHVl
        Ck5vZGVTdHJ1Y3QBBBpUb3VraS5SZXNvdXJjZXMuTm9kZVN0cnVjdAIAAAACAAAABgMAAAAEcm9vdAX8////GlRvdWtpLlJl
        c291cmNlcy5Ob2RlU3RydWN0AQAAAAROb2RlBCJUb3VraS5SZXNvdXJjZXMuTm9kZVdpdGhOb2RlU3RydWN0AgAAAAIAAAAJ
        AQAAAAs=
        """;

    internal const string ArrayBackReference = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAcBAAAAAAEAAAACAAAABCJUb3VraS5SZXNvdXJjZXMuQXJyYXlCYWNrUmVmZXJlbmNl
        AgAAAAX9////IlRvdWtpLlJlc291cmNlcy5BcnJheUJhY2tSZWZlcmVuY2UCAAAABVZhbHVlBUFycmF5AAQIJFRvdWtpLlJl
        c291cmNlcy5BcnJheUJhY2tSZWZlcmVuY2VbXQIAAAACAAAACwAAAAkBAAAAAfv////9////FgAAAAkBAAAACw==
        """;

    internal const string ObjectReferenceSingleton = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAKFRvdWtpLlJlc291cmNlcy5PYmplY3RSZWZlcmVuY2VTaW5nbGV0b24AAAAA
        AgAAAAs=
        """;

    internal const string NullObjectReferenceContainer = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAFpOdWxsT2JqZWN0UmVmZXJlbmNlRml4dHVyZUdlbmVyYXRvciwgVmVyc2lvbj0wLjAuMC4w
        LCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPW51bGwFAQAAACxUb3VraS5SZXNvdXJjZXMuTnVsbE9iamVjdFJlZmVy
        ZW5jZUNvbnRhaW5lcgEAAAAFVmFsdWUEI1RvdWtpLlJlc291cmNlcy5OdWxsT2JqZWN0UmVmZXJlbmNlAgAAAAIAAAAJAwAAAAUD
        AAAAI1RvdWtpLlJlc291cmNlcy5OdWxsT2JqZWN0UmVmZXJlbmNlAQAAAAZNYXJrZXIACAIAAAAqAAAACw==
        """;

    internal const string NullMemberObjectReference = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAGBOdWxsTWVtYmVyT2JqZWN0UmVmZXJlbmNlRml4dHVyZUdlbmVyYXRvciwgVmVyc2lvbj0w
        LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPW51bGwFAQAAAClUb3VraS5SZXNvdXJjZXMuTnVsbE1lbWJl
        ck9iamVjdFJlZmVyZW5jZQEAAAAGTWVtYmVyAgIAAAAJAwAAAAUDAAAAKVRvdWtpLlJlc291cmNlcy5OdWxsT2JqZWN0UmVmZXJl
        bmNlTWVtYmVyAAAAAAIAAAAL
        """;

    internal const string NullableSerializablePayloadNull = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAK1RvdWtpLlJlc291cmNlcy5OdWxsYWJsZVNlcmlhbGl6YWJsZVBheWxvYWQB
        AAAABVZhbHVlA25TeXN0ZW0uTnVsbGFibGVgMVtbU3lzdGVtLkludDMyLCBtc2NvcmxpYiwgVmVyc2lvbj00LjAuMC4wLCBD
        dWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWI3N2E1YzU2MTkzNGUwODldXQIAAAAKCw==
        """;

    internal const string NullableSerializablePayloadValue = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAK1RvdWtpLlJlc291cmNlcy5OdWxsYWJsZVNlcmlhbGl6YWJsZVBheWxvYWQB
        AAAABVZhbHVlAwxTeXN0ZW0uSW50MzICAAAACAgqAAAACw==
        """;

    internal const string RegisteredPayloadMatrix = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAcBAAAAAgIAAAABAAAAAgAAAAQhVG91a2kuUmVzb3VyY2VzLlJlZ2lzdGVyZWRQYXls
        b2FkAgAAAAkDAAAACQQAAAAFAwAAACFUb3VraS5SZXNvdXJjZXMuUmVnaXN0ZXJlZFBheWxvYWQDAAAABE5hbWUGTnVtYmVy
        BE5leHQBAAQIIVRvdWtpLlJlc291cmNlcy5SZWdpc3RlcmVkUGF5bG9hZAIAAAACAAAABgUAAAAEbGVmdAEAAAAKAQQAAAAD
        AAAABgYAAAAFcmlnaHQCAAAACgs=
        """;

    internal const string CallbackStructField = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAJ1RvdWtpLlJlc291cmNlcy5DYWxsYmFja1N0cnVjdENvbnRhaW5lcgEAAAAF
        VmFsdWUEHlRvdWtpLlJlc291cmNlcy5DYWxsYmFja1N0cnVjdAIAAAACAAAABf3///8eVG91a2kuUmVzb3VyY2VzLkNhbGxi
        YWNrU3RydWN0AQAAAAVWYWx1ZQAIAgAAACoAAAAL
        """;

    internal const string CallbackStructArray = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAcBAAAAAAEAAAACAAAABB5Ub3VraS5SZXNvdXJjZXMuQ2FsbGJhY2tTdHJ1Y3QCAAAA
        Bf3///8eVG91a2kuUmVzb3VyY2VzLkNhbGxiYWNrU3RydWN0AQAAAAVWYWx1ZQAIAgAAACoAAAAB/P////3///8rAAAACw==
        """;

    internal const string FanOutPayload = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAHVRvdWtpLlJlc291cmNlcy5GYW5PdXRQYXlsb2FkAgAAAAVGaXJzdAZTZWNv
        bmQEBBtUb3VraS5SZXNvdXJjZXMuRmFuT3V0T3duZXICAAAAG1RvdWtpLlJlc291cmNlcy5GYW5PdXRPd25lcgIAAAACAAAA
        CQMAAAAJBAAAAAUDAAAAG1RvdWtpLlJlc291cmNlcy5GYW5PdXRPd25lcgEAAAAFVmFsdWUEG1RvdWtpLlJlc291cmNlcy5G
        YW5PdXRWYWx1ZQIAAAACAAAACQUAAAABBAAAAAMAAAAJBQAAAAUFAAAAG1RvdWtpLlJlc291cmNlcy5GYW5PdXRWYWx1ZQEA
        AAAFVmFsdWUACAIAAAAqAAAACw==
        """;

    internal const string ArrayList = """
        AAEAAAD/////AQAAAAAAAAAEAQAAABxTeXN0ZW0uQ29sbGVjdGlvbnMuQXJyYXlMaXN0AwAAAAZfaXRlbXMFX3NpemUIX3Zl
        cnNpb24FAAAICAkCAAAAAwAAAAMAAAAQAgAAAAQAAAAICAEAAAAGAwAAAAN0d28NAgs=
        """;

    internal const string Hashtable = """
        AAEAAAD/////AQAAAAAAAAAEAQAAABxTeXN0ZW0uQ29sbGVjdGlvbnMuSGFzaHRhYmxlBwAAAApMb2FkRmFjdG9yB1ZlcnNp
        b24IQ29tcGFyZXIQSGFzaENvZGVQcm92aWRlcghIYXNoU2l6ZQRLZXlzBlZhbHVlcwAAAwMABQULCBxTeXN0ZW0uQ29sbGVj
        dGlvbnMuSUNvbXBhcmVyJFN5c3RlbS5Db2xsZWN0aW9ucy5JSGFzaENvZGVQcm92aWRlcgjsUTg/AgAAAAoKAwAAAAkCAAAA
        CQMAAAAQAgAAAAIAAAAGBAAAAANvbmUGBQAAAAN0d28QAwAAAAIAAAAICAEAAAAICAIAAAAL
        """;

    internal const string ConvergingCallbackStruct = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAK1RvdWtpLlJlc291cmNlcy5Db252ZXJnaW5nQ2FsbGJhY2tDb250YWluZXIB
        AAAABVZhbHVlBChUb3VraS5SZXNvdXJjZXMuQ29udmVyZ2luZ0NhbGxiYWNrU3RydWN0AgAAAAIAAAAF/f///yhUb3VraS5S
        ZXNvdXJjZXMuQ29udmVyZ2luZ0NhbGxiYWNrU3RydWN0AgAAAAVGaXJzdAZTZWNvbmQCAgIAAAAJBAAAAAkEAAAABQQAAAAe
        VG91a2kuUmVzb3VyY2VzLkNhbGxiYWNrU3RydWN0AQAAAAVWYWx1ZQAIAgAAACoAAAAL
        """;

    internal static BinaryFormattedObject Parse(
        string payload,
        RegisteredTypeResolver? typeResolver = null)
    {
        using MemoryStream stream = new(Convert.FromBase64String(payload));
        return new BinaryFormattedObject(stream, typeResolver);
    }
}