﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupery
{
    class PersonalityManager
    {
        public const string IMPORTED_PERSONALITIES_FILE_NAME = "dupery.PERSONALITIES.json";
        public const string OVERRIDE_PERSONALITIES_FILE_NAME = "OVERRIDE.{0}.json";

        private const string PERSONALITIES_FILE_NAME = "PERSONALITIES.json";
        private const int MINIMUM_PERSONALITY_COUNT = 4;

        private readonly string personalitiesFilePath;

        private Dictionary<string, PersonalityOutline> storedPersonalities;
        private Dictionary<string, Dictionary<string, PersonalityOutline>> importedPersonalities;
        private bool newFileNeeded = false;

        public PersonalityManager()
        {
            this.personalitiesFilePath = Path.Combine(DuperyPatches.DirectoryName, PERSONALITIES_FILE_NAME);

            this.storedPersonalities = new Dictionary<string, PersonalityOutline>();
            this.importedPersonalities = new Dictionary<string, Dictionary<string, PersonalityOutline>>();

            TryFetchPersonalities();
        }

        public List<Personality> GetPersonalities()
        {
            // Fetch stored personalities now if it hasn't already happened
            if (this.newFileNeeded)
            {
                InitializeDefaultPersonalityFile();
                FetchPersonalities();

                this.newFileNeeded = false;
            }

            List<Personality> personalities = new List<Personality>();

            foreach (string key in storedPersonalities.Keys)
                if (storedPersonalities[key].Enabled)
                    personalities.Add(storedPersonalities[key].toPersonality(key));

            foreach (Dictionary<string, PersonalityOutline> personalityMap in importedPersonalities.Values)
                foreach (string key in personalityMap.Keys)
                    if (personalityMap[key].Enabled)
                        personalities.Add(personalityMap[key].toPersonality(key));

            while (personalities.Count < MINIMUM_PERSONALITY_COUNT)
            {
                string name = string.Format("ULTRADUPE-{0}{1:00000}", personalities.Count + 1, UnityEngine.Random.Range(0, 100000));
                Personality substitutePersonality = PersonalityGenerator.randomPersonality(name, "STRINGS.BAD_DUPLICANT_DESCRIPTION");

                Debug.Log($"Not enough personalities, adding {name} to personality pool.");
                personalities.Add(substitutePersonality);
            }

            return personalities;
        }

        public static Dictionary<string, PersonalityOutline> ReadPersonalities(string personalitiesFilePath)
        {
            Dictionary<string, PersonalityOutline> jsonPersonalities;
            using (StreamReader streamReader = new StreamReader(personalitiesFilePath))
                jsonPersonalities = JsonConvert.DeserializeObject<Dictionary<string, PersonalityOutline>>(streamReader.ReadToEnd());

            return jsonPersonalities;
        }

        public static void WritePersonalities(string personalitiesFilePath, Dictionary<string, PersonalityOutline> jsonPersonalities)
        {
            using (StreamWriter streamWriter = new StreamWriter(personalitiesFilePath))
            {
                string json = JsonConvert.SerializeObject(jsonPersonalities, Formatting.Indented);
                streamWriter.Write(json);
            }
        }

        private void TryFetchPersonalities()
        {
            if (!File.Exists(this.personalitiesFilePath))
            {
                Debug.Log($"{PERSONALITIES_FILE_NAME} not found, a fresh one will be generated.");
                this.newFileNeeded = true;
                return;
            }
            else
            {
                FetchPersonalities();
            }
        }

        private void FetchPersonalities()
        {
            Debug.Log($"Reading personalities from {PERSONALITIES_FILE_NAME}...");
            storedPersonalities = ReadPersonalities(this.personalitiesFilePath);
            Debug.Log($"Personalities fetched.");
        }

        private void InitializeDefaultPersonalityFile()
        {
            Dictionary<string, PersonalityOutline> dbPersonalities = new Dictionary<string, PersonalityOutline>();

            int personalitiesCount = Db.Get().Personalities.Count;
            for (int i = 0; i < personalitiesCount; i++)
            {
                Personality dbPersonality = Db.Get().Personalities[i];
                dbPersonalities[dbPersonality.nameStringKey] = PersonalityOutline.fromStockPersonality(dbPersonality);
            }

            Debug.Log($"Writing initial {personalitiesCount} personalities file...");
            WritePersonalities(this.personalitiesFilePath, dbPersonalities);
            Debug.Log($"Default personalities file initialized.");
        }

        public void TryImportPersonalities(string importFilePath, string modId)
        {
            this.importedPersonalities[modId] = ReadPersonalities(importFilePath);
            Debug.Log($"{importedPersonalities[modId].Count} personalities imported successfully.");

            string overrideFilePath = Path.Combine(DuperyPatches.DirectoryName, string.Format(OVERRIDE_PERSONALITIES_FILE_NAME, modId));
            Dictionary<string, PersonalityOutline> currentOverrides = null;
            if (File.Exists(overrideFilePath))
            {
                currentOverrides = ReadPersonalities(overrideFilePath);
            }

            Dictionary<string, PersonalityOutline> newOverrides = new Dictionary<string, PersonalityOutline>();
            foreach (string key in importedPersonalities[modId].Keys)
            {
                PersonalityOutline overridingPersonality = null;
                if (currentOverrides != null)
                    currentOverrides.TryGetValue(key, out overridingPersonality);

                if (overridingPersonality != null)
                {
                    importedPersonalities[modId][key].OverrideValues(overridingPersonality);
                    newOverrides[key] = overridingPersonality;
                }
                else
                {
                    newOverrides[key] = new PersonalityOutline { Enabled = importedPersonalities[modId][key].Enabled };
                }
            }

            WritePersonalities(overrideFilePath, newOverrides);
        }
    }
}
