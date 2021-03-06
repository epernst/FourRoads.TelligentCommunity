using FourRoads.Common.TelligentCommunity.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Telligent.Evolution.Extensibility;
using Telligent.Evolution.Extensibility.Api.Version1;
using Telligent.Evolution.Extensibility.Rules.Version1;
using Telligent.Evolution.Extensibility.Version1;
using Telligent.Evolution.Forums.Internal.ReplyVote;
using ForumReplyVote = Telligent.Evolution.Extensibility.Api.Entities.Version1.ForumReplyVote;

namespace FourRoads.TelligentCommunity.Rules.Triggers
{
    public class ForumReplyUpVoteCancel : IRuleTrigger, ITranslatablePlugin, ISingletonPlugin, ICategorizedPlugin
    {
        private IRuleController _ruleController;
        private ITranslatablePluginController _translationController;
        private readonly Guid _triggerid = new Guid("{B15276E9-4AF7-4574-8698-40CA6F1A1484}");

        private ConcurrentDictionary<string, ForumReplyVote>
            _beforeUpdateCache = new ConcurrentDictionary<string, ForumReplyVote>();

        public void Initialize()
        {
            Apis.Get<IForumReplyVotes>().Events.BeforeCreate += EventsOnBeforeCreate;
            Apis.Get<IForumReplyVotes>().Events.AfterCreate += EventsOnAfterCreate;
            Apis.Get<IForumReplyVotes>().Events.AfterDelete += EventsOnAfterDelete;
            Apis.Get<IForumReplyVotes>().Events.BeforeUpdate += EventsOnBeforeUpdate;
            Apis.Get<IForumReplyVotes>().Events.AfterUpdate += EventsOnAfterUpdate;
        }

        /// <summary>
        /// Save a version of the vote so that we can determine what actually happened 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private void EventsOnBeforeCreate(ForumReplyVoteBeforeCreateEventArgs args)
        {
            try
            {
                if (args.VoteType == ForumReplyVoteType.Quality)
                {
                    CacheUserVote(args.ReplyId, args.UserId);
                }
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnBeforeCreate failed for userid:{0}",
                        args.UserId), ex).Log();
            }
        }

        /// <summary>
        /// Check on the action performed
        /// For some reason this event is also called when updating ?
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private void EventsOnAfterCreate(ForumReplyVoteAfterCreateEventArgs args)
        {
            try
            {
                if (args.VoteType == ForumReplyVoteType.Quality && _ruleController != null)
                {
                    string key = args.UserId + "," + args.ReplyId;
                    if (_beforeUpdateCache.ContainsKey(key))
                    {
                        var old = _beforeUpdateCache[key];
                        ForumReplyVote removed;
                        _beforeUpdateCache.TryRemove(key, out removed);

                        //amended - was up vote and is now down vote
                        if (old.Value && !args.Value)
                        {
                            _ruleController.ScheduleTrigger(new Dictionary<string, string>()
                            {
                                {
                                    "UserId", args.UserId.ToString()
                                },
                                {
                                    "ReplyId", args.ReplyId.ToString()
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnAfterCreate failed for userid:{0}", args.UserId),
                    ex).Log();
            }
        }

        /// <summary>
        /// Determine what was removed
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private void EventsOnAfterDelete(ForumReplyVoteAfterDeleteEventArgs args)
        {
            try
            {
                if (args.VoteType == ForumReplyVoteType.Quality && _ruleController != null)
                {
                    // removing up vote
                    if (args.Value)
                    {
                        _ruleController.ScheduleTrigger(new Dictionary<string, string>()
                        {
                            {
                                "UserId", args.UserId.ToString()
                            },
                            {
                                "ReplyId", args.ReplyId.ToString()
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnAfterDelete failed for userid:{0}", args.UserId),
                    ex).Log();
            }
        }

        /// <summary>
        /// Save a version of the vote so that we can determine what actually happened 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private void EventsOnBeforeUpdate(ForumReplyVoteBeforeUpdateEventArgs args)
        {
            try
            {
                if (args.VoteType == ForumReplyVoteType.Quality)
                {
                    CacheUserVote(args.ReplyId, args.UserId);
                }
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnBeforeUpdate failed for userid:{0}",
                        args.UserId), ex).Log();
            }
        }

        /// <summary>
        /// Check on the action performed
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private void EventsOnAfterUpdate(ForumReplyVoteAfterUpdateEventArgs args)
        {
            try
            {
                if (args.VoteType == ForumReplyVoteType.Quality && _ruleController != null)
                {
                    string key = args.UserId + "," + args.ReplyId;
                    if (_beforeUpdateCache.ContainsKey(key))
                    {
                        var old = _beforeUpdateCache[key];
                        ForumReplyVote removed;
                        _beforeUpdateCache.TryRemove(key, out removed);

                        //amended - was up vote and is now down vote
                        if (old.Value && !args.Value)
                        {
                            _ruleController.ScheduleTrigger(new Dictionary<string, string>()
                            {
                                {
                                    "UserId", args.UserId.ToString()
                                },
                                {
                                    "ReplyId", args.ReplyId.ToString()
                                }
                            });
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnAfterUpdate failed for userid:{0}", args.UserId),
                    ex).Log();
            }
        }

        /// <summary>
        /// Cache a copy of the vote before it is updated so that we can later determine what happened
        /// </summary>
        /// <param name="replyId"></param>
        /// <param name="userId"></param>
        /// <returns>bool</returns>
        /// 
        private bool CacheUserVote(int replyId, int userId)
        {
            if (!_beforeUpdateCache.ContainsKey(userId + "," + replyId))
            {
                var vote = Apis.Get<IForumReplyVotes>()
                    .Get(replyId, new ForumReplyVoteGetOptions() { VoteType = "Quality" });

                if (vote != null && !vote.HasErrors())
                {
                    _beforeUpdateCache.AddOrUpdate(userId + "," + replyId, vote, (key, existingVal) => vote);
                }
            }

            return true;
        }

        public string Name
        {
            get { return "4 Roads - Forum Reply Up Vote Cancelled Trigger"; }
        }

        public string Description
        {
            get { return "Fires when a reply's up vote is cancelled by a user"; }
        }

        public void SetController(IRuleController controller)
        {
            _ruleController = controller;
        }

        public RuleTriggerExecutionContext GetExecutionContext(RuleTriggerData data)
        {
            RuleTriggerExecutionContext context = new RuleTriggerExecutionContext();
            if (data.ContainsKey("UserId"))
            {
                int userId;

                if (int.TryParse(data["UserId"], out userId))
                {
                    var users = Apis.Get<IUsers>();
                    var user = users.Get(new UsersGetOptions() { Id = userId });

                    if (!user.HasErrors())
                    {
                        context.Add(users.ContentTypeId, user);
                        context.Add(_triggerid, true); //Added this trigger so that it is not re-entrant
                    }
                }
            }

            if (data.ContainsKey("ReplyId"))
            {
                int replyId;
                if (int.TryParse(data["ReplyId"], out replyId))
                {
                    var forumReplies = Apis.Get<IForumReplies>();
                    var forumReply = forumReplies.Get(replyId);

                    if (!forumReply.HasErrors())
                    {
                        context.Add(forumReply.GlobalContentTypeId, forumReply);
                    }
                }
            }
            return context;
        }

        public Guid RuleTriggerId
        {
            get { return _triggerid; }
        }

        public string RuleTriggerName
        {
            get { return _translationController.GetLanguageResourceValue("RuleTriggerName"); }
        }

        public string RuleTriggerCategory
        {
            get { return _translationController.GetLanguageResourceValue("RuleTriggerCategory"); }
        }

        /// <summary>
        /// Setup the contextual datatype ids for users, forum reply and custom trigger parameters
        /// custom trigger parameters are to allow config in the UI for the action being performed (add-upvote, del-upvote etc)
        /// </summary>
        /// <returns>IEnumerable<Guid></returns>
        /// 
        public IEnumerable<Guid> ContextualDataTypeIds
        {
            get { return new[] { Apis.Get<IUsers>().ContentTypeId, Apis.Get<IForumReplies>().ContentTypeId }; }
        }

        public void SetController(ITranslatablePluginController controller)
        {
            _translationController = controller;
        }

        public Translation[] DefaultTranslations
        {
            get
            {
                Translation[] defaultTranslation = new[] { new Translation("en-us") };

                defaultTranslation[0].Set("RuleTriggerName", "a forum reply up vote was cancelled - 4 roads");
                defaultTranslation[0].Set("RuleTriggerCategory", "Forum Reply");

                return defaultTranslation;
            }
        }

        public string[] Categories
        {
            get
            {
                return new[]
                {
                    "Rules"
                };
            }
        }

    }
}
