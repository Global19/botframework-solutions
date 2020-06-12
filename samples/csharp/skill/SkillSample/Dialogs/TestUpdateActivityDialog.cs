﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Solutions.Responses;
using SkillSample.Models;
using SkillSample.Services;

namespace SkillSample.Dialogs
{
    public class TestUpdateActivityDialog : SkillDialogBase
    {
        protected readonly string _dialogId;
        private readonly SkillState _stateAccesor;
        private readonly LocaleTemplateManager _templateEngine;
        private readonly IBotTelemetryClient _telemetryClient;
        private static readonly IServiceProvider _serviceProvider;
        private static readonly string CARD_ACTIVITY_IDENTIFIER = "testUpdateCard";

        public TestUpdateActivityDialog(
            string dialogId,
            IServiceProvider serviceProvider,
            SkillState stateAccesor,
            BotSettings settings,
            BotServices services,
            LocaleTemplateManager templateEngine,
            IBotTelemetryClient telemetryClient)
            : base(dialogId, serviceProvider)
        {
            _stateAccesor = stateAccesor;
            _templateEngine = templateEngine;
        }

        // Begin Dialog method
        public override async Task<DialogTurnResult> BeginDialogAsync(DialogContext innerDc, object options, CancellationToken cancellationToken = default)
        {
            var skillState = await this.StateAccessor.GetAsync(innerDc.Context, () => new SkillState());
            skillState.CardsToUpdate = new Activity();
            var act = this.TemplateEngine.GenerateActivityForLocale("TestCard", { Name: "Send Activity"});
            this.RegisterActivityListener(innerDc);
            if (act.Attachments)
            {
                await this.SendOrUpdateCardAsync(innerDc, act.Attachments[0], TestUpdateActivityDialog.CARD_ACTIVITY_IDENTIFIER);
            }
            else
            {
                await innerDc.Context.SendActivityAsync(this.TemplateEngine.GenerateActivityForLocale("UnsupportedMessage"));
            }

            return Dialog.EndOfTurn;
        }

        // Performing some action
        public override async Task<DialogTurnResult> ContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
        {
            var act = this.TemplateEngine.GenerateActivityForLocale(("TestCard", { Name: "Update Activity"});
            if (act.Attachments)
            {
                await this.SendOrUpdateCardAsync(innerDc, act.Attachments[0], TestUpdateActivityDialog.CARD_ACTIVITY_IDENTIFIER);
            }
            else
            {
                await innerDc.Context.SendActivityAsync(this.TemplateEngine.GenerateActivityForLocale("UnsupportedMessage"));
            }

            return Dialog.EndOfTurn;
        }

        /**
        * Send / Update a card with a given name
        * @param card the actual card to send/update
        * @param activityIdentifier name of the card to be sent, used to look up its corresponding activity to see if it was sent before
        * @param forceSend whether to force the send of a new card even if a card with the same name was sent before (i.e do not update)
        */
        private async Task SendOrUpdateCardAsync(DialogContext innerDc, Attachment card, string activityIdentifier)
        {
            var skillState = await this.StateAccessor.GetAsync(innerDc, () => new SkillState());
            var previouslySentActivity = skillState.CardsToUpdate[activityIdentifier];

            if (previouslySentActivity == null || innerDc.Context.Activity.ChannelId != "msteams")
            {
                // send a new card and set the activityName so that our listener knows that this is an activity we want to keep
                var responseToUser = MessageFactory.Attachment(card);
                responseToUser.ChannelData.ActivityName = activityIdentifier;

                var cardResponse = await innerDc.Context.SendActivityAsync(responseToUser);

                // the previouslySentActivity should now have been filled by the onSendActivities listener
                previouslySentActivity = skillState.CardsToUpdate[activityIdentifier];

                // store the activity id, which we cannot get in the listener which we register in beginDialog
                if (cardResponse && previouslySentActivity)
                {
                    previouslySentActivity.Id = cardResponse.Id;
                }
            }
            else
            {
                previouslySentActivity.attachments = [card];
                await innerDc.Context.UpdateActivityAsync(previouslySentActivity);
            }
        }

        private Task RegisterActivityListener(DialogContext innerDc)
        {
            // listen to the activities being sent and save them so we can update them later
            // for updating an activity, we need:
            // - the activity id
            // - the conversation id
            // - the service URL
            // which we unfortunately cannot get all just from the response to the sendActivity, so we must use a listener here
            // we cannot get the activity id from this listener however, so we have to get it from the response

            /**
            innerDc.Context.OnSendActivities(async(TurnContext context, Activity activities, nextSend: () => Promise<ResourceResponse[]>) => {
                activities.forEach(async(activity: Partial<Activity>) => {
                    var state = await this.StateAccessor.GetAsync(innerDc.Context);
                    if (activity.channelData && activity.channelData.activityName)
                    {
                        state.cardsToUpdate[activity.channelData.activityName] = activity;
                    }
                });

                return nextSend();
            });
            */
        }
    }
}