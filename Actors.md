# Actor System Design

* ScrumBot
    * Parent of all other actors
* SlackConnector
    * Handles the Slack messaging
* ExecutiveAdministrator
    * Handles interactions for configuration
        * Keeps track of users who want to be interviewed and when
        * Keeps track of which rooms to report to and when
        * Keeps track of reports
    * Examples:
        * Help
        * @scrumbot, add me to the engineering team
        * Add me to the sales team, @scrumbot
        * Add @fwise to the executive team
        * Report the engineering team status in #e-standup
        * Give a report for the sales team in #sales at 10:15 AM
        * Tell me the report for engineering
    * User
        * Team
        * Slack user ID
        * Time of day
        * Days of week
    * Reporter
        * Team
        * Slack channel ID
        * Time of day
        * Days of week
* Interviewer
    * One instance per user
    * Interviews (asks questions of) each scrum participant
    * Delivers report to ExecutiveAdministrator
* Reporter
    * One per team
    * Compiles reports held by ExecutiveAdministrator
    * Posts report to room