using System;
using System.Collections.Generic;
using System.Text;

namespace CommunityBot.Features.Lists
{
    public class ListException : Exception
    {
        public ListException(string message) : base(message)
        {
        }

        public class ListManagerException : ListException
        {
            public ListManagerException(string message) : base(message)
            {
            }
        }

        public class ListPermissionException : ListException
        {
            public ListPermissionException(string message) : base(message)
            {
            }
        }
    }
}
