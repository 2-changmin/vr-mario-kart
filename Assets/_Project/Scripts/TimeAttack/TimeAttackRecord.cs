using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRKart.TimeAttack
{
    // 고스트 한 프레임: 레이스 시간(초)과 카트 루트 위치·회전
    public struct GhostSample
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;

        public GhostSample(float time, Vector3 position, Quaternion rotation)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
        }
    }

    // 트랙 하나의 타임어택 기록 (#47). 최고 전체 기록을 낸 주행의 랩 구간 기록(누적)과 고스트 샘플, 그리고 따로 가장 빠른 랩 기록.
    // persistentDataPath/TimeAttack/<씬 이름>.ghost 에 바이너리로 저장한다 (20Hz, 1분 ≈ 1,200 샘플 ≈ 38KB).
    public sealed class TimeAttackRecord
    {
        private const int Magic = 0x4847_4B56;   // "VKGH"
        private const int Version = 1;

        public int LapCount;
        public float BestTotal;
        public float BestLap;
        public readonly List<float> Splits = new List<float>();         // 각 랩을 끝낸 순간의 레이스 시간 (최고 전체 기록 주행)
        public readonly List<GhostSample> Samples = new List<GhostSample>();

        public static string PathFor(string trackScene) =>
            Path.Combine(Application.persistentDataPath, "TimeAttack", trackScene + ".ghost");

        // 파일이 없거나 깨졌으면 null
        public static TimeAttackRecord Load(string trackScene)
        {
            string path = PathFor(trackScene);
            if (!File.Exists(path)) return null;
            try
            {
                using var reader = new BinaryReader(File.OpenRead(path));
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return null;

                var record = new TimeAttackRecord
                {
                    LapCount = reader.ReadInt32(),
                    BestTotal = reader.ReadSingle(),
                    BestLap = reader.ReadSingle(),
                };
                int splitCount = reader.ReadInt32();
                for (int i = 0; i < splitCount; i++) record.Splits.Add(reader.ReadSingle());
                int sampleCount = reader.ReadInt32();
                for (int i = 0; i < sampleCount; i++)
                {
                    float time = reader.ReadSingle();
                    var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    record.Samples.Add(new GhostSample(time, position, rotation));
                }
                return record;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[TimeAttackRecord] '{path}' 기록을 읽지 못했습니다: {e.Message}");
                return null;
            }
        }

        public void Save(string trackScene)
        {
            string path = PathFor(trackScene);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using var writer = new BinaryWriter(File.Create(path));
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(LapCount);
                writer.Write(BestTotal);
                writer.Write(BestLap);
                writer.Write(Splits.Count);
                foreach (float split in Splits) writer.Write(split);
                writer.Write(Samples.Count);
                foreach (GhostSample sample in Samples)
                {
                    writer.Write(sample.Time);
                    writer.Write(sample.Position.x);
                    writer.Write(sample.Position.y);
                    writer.Write(sample.Position.z);
                    writer.Write(sample.Rotation.x);
                    writer.Write(sample.Rotation.y);
                    writer.Write(sample.Rotation.z);
                    writer.Write(sample.Rotation.w);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[TimeAttackRecord] '{path}' 기록을 저장하지 못했습니다: {e.Message}");
            }
        }
    }
}
