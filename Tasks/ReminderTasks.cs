﻿namespace Cliptok.Tasks
{
    internal class ReminderTasks
    {
        public static async Task<bool> CheckRemindersAsync()
        {
            bool success = false;
            foreach (var reminder in Program.redis.ListRange("reminders", 0, -1))
            {
                bool DmFallback = false;
                var reminderObject = JsonConvert.DeserializeObject<Commands.GlobalCmds.Reminder>(reminder);
                if (reminderObject.ReminderTime <= DateTime.Now)
                {
                    var user = await Program.discord.GetUserAsync(reminderObject.UserID);
                    DiscordChannel channel = null;
                    try
                    {
                        channel = await Program.discord.GetChannelAsync(reminderObject.ChannelID);
                    }
                    catch
                    {
                        // channel likely doesnt exist
                    }
                    if (channel is null)
                    {
                        var guild = Program.homeGuild;
                        var member = await guild.GetMemberAsync(reminderObject.UserID);

                        if ((await GetPermLevelAsync(member)) >= ServerPermLevel.TrialModerator)
                        {
                            channel = await Program.discord.GetChannelAsync(Program.cfgjson.HomeChannel);
                        }
                        else
                        {
                            channel = await member.CreateDmChannelAsync();
                            DmFallback = true;
                        }
                    }

                    await Program.redis.ListRemoveAsync("reminders", reminder);
                    success = true;

                    var embed = new DiscordEmbedBuilder()
                    .WithDescription(reminderObject.ReminderText)
                    .WithColor(new DiscordColor(0xD084))
                    .WithFooter(
                        "Reminder was set",
                        null
                    )
                    .WithTimestamp(reminderObject.OriginalTime)
                    .WithAuthor(
                        $"Reminder from {TimeHelpers.TimeToPrettyFormat(DateTime.Now.Subtract(reminderObject.OriginalTime), true)}",
                        null,
                        user.AvatarUrl
                    )
                    .AddField("Context", $"{reminderObject.MessageLink}", true);

                    var msg = new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .WithContent($"<@{reminderObject.UserID}>, you asked to be reminded of something:");

                    if (DmFallback)
                    {
                        msg.WithContent("You asked to be reminded of something:");
                        await channel.SendMessageAsync(msg);
                    }
                    else if (reminderObject.MessageID != default)
                    {
                        try
                        {
                            msg.WithReply(reminderObject.MessageID, mention: true, failOnInvalidReply: true)
                                .WithContent("You asked to be reminded of something:");
                            await channel.SendMessageAsync(msg);
                        }
                        catch (DSharpPlus.Exceptions.BadRequestException)
                        {
                            msg.WithContent($"<@{reminderObject.UserID}>, you asked to be reminded of something:");
                            msg.WithReply(null);
                            msg.WithAllowedMentions(Mentions.All);
                            await channel.SendMessageAsync(msg);
                        }
                    }
                    else
                    {
                        await channel.SendMessageAsync(msg);
                    }
                }

            }
            Program.discord.Logger.LogDebug(Program.CliptokEventID, "Checked reminders at {time} with result: {success}", DateTime.Now, success);
            return success;
        }

    }
}
