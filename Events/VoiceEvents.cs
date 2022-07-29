﻿namespace Cliptok.Events
{
    public class VoiceEvents
    {
        private static List<ulong> PendingOverWrites = new();

        public static async Task VoiceStateUpdate(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            if (e.After.Channel is null)
            {
                client.Logger.LogDebug($"{e.User.Username} left {e.Before.Channel.Name}");

                UserLeft(client, e);
            }
            else if (e.Before is null)
            {
                client.Logger.LogDebug($"{e.User.Username} joined {e.After.Channel.Name}");

                UserJoined(client, e);
            }
            else if (e.Before.Channel.Id != e.After.Channel.Id)
            {
                client.Logger.LogDebug($"{e.User.Username} moved from {e.Before.Channel.Name} to {e.After.Channel.Name}");

                UserLeft(client, e);
                UserJoined(client, e);
            }

            if (e.Before is not null && e.Before.Channel.Users.Count == 0)
            {
                client.Logger.LogDebug($"{e.Before.Channel.Name} is now empty!");

                // todo: purge message history, on delay
            }

        }

        public static async Task UserJoined(DiscordClient _, VoiceStateUpdateEventArgs e)
        {

            while (PendingOverWrites.Contains(e.User.Id))
            {
                Console.WriteLine("spinning");
                await Task.Delay(5);
            }

            //if (PendingOverWriteAdds.GetValueOrDefault(e.User.Id) == 0)
            PendingOverWrites.Add(e.User.Id);
            //else
            //    PendingOverWriteAdds[e.User.Id] = PendingOverWriteAdds[e.User.Id] + 1;

            DiscordOverwrite[] existingOverwrites = e.After.Channel.PermissionOverwrites.ToArray();

            if (!e.After.Member.Roles.Any(role => role.Id == Program.cfgjson.MutedRole))
            {
                bool userOverrideSet = false;
                foreach (DiscordOverwrite overwrite in existingOverwrites)
                {
                    if (overwrite.Type == OverwriteType.Member && overwrite.Id == e.After.Member.Id)
                    {
                        await e.After.Channel.AddOverwriteAsync(e.After.Member, overwrite.Allowed | Permissions.SendMessages, overwrite.Denied, "User joined voice channel.");
                        userOverrideSet = true;
                        break;
                    }
                }

                if (!userOverrideSet)
                {
                    await e.After.Channel.AddOverwriteAsync(e.After.Member, Permissions.SendMessages, Permissions.None, "User joined voice channel.");
                }
            }

            PendingOverWrites.Remove(e.User.Id);

            DiscordMessageBuilder message = new()
            {
                Content = $"{e.After.Member.Mention} has joined."
            };
            await e.After.Channel.SendMessageAsync(message.WithAllowedMentions(Mentions.None));
        }

        public static async Task UserLeft(DiscordClient _, VoiceStateUpdateEventArgs e)
        {
            Task.Run(async () =>
            {
                DiscordMember member;
                if (e.After.Channel is null)
                    member = e.Before.Member;
                else
                    member = e.After.Member;

                while (PendingOverWrites.Contains(e.User.Id));
                {
                    Console.WriteLine("spinning");
                    await Task.Delay(5);
                }

                PendingOverWrites.Add(e.User.Id);

                DiscordOverwrite[] existingOverwrites = e.Before.Channel.PermissionOverwrites.ToArray();

                foreach (DiscordOverwrite overwrite in existingOverwrites)
                {
                    if (overwrite.Type == OverwriteType.Member && overwrite.Id == member.Id)
                    {
                        if (overwrite.Allowed == Permissions.SendMessages && overwrite.Denied == Permissions.None)
                        {
                            // User only has allow for Send Messages, so we can delete the entire override
                            await overwrite.DeleteAsync("User left voice channel.");
                        }
                        else
                        {
                            // User has other overrides set, so we should only remove the Send Messages override
                            if (overwrite.Allowed.HasPermission(Permissions.SendMessages))
                            {
                                await e.Before.Channel.AddOverwriteAsync(member, (Permissions)(overwrite.Allowed - Permissions.SendMessages), overwrite.Denied, "User left voice channel.");
                            }
                            else
                            {
                                // Check if the overwrite has no permissions set - if so, delete it to keep the list clean.
                                if (overwrite.Allowed == Permissions.None && overwrite.Denied == Permissions.None)
                                {
                                    await overwrite.DeleteAsync("User left voice channel.");
                                }
                            }
                        }
                        break;
                    }
                }

                PendingOverWrites.Remove(e.User.Id);

                DiscordMessageBuilder message = new()
                {
                    Content = $"{member.Mention} has left."
                };
                await e.Before.Channel.SendMessageAsync(message.WithAllowedMentions(Mentions.None));
            });
        }
    }
}
