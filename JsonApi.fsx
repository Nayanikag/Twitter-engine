// depending on the func value the input can be tweet, hashtag, mention, subscribe or unsubscribe data. 
type Request = {
        func: string;
        username: string;
        input: string
    }

// Any response from the server follows the bellow structure. Values irrelevant to a particular cunstion are sent as none. 
type Response = {
        func: string;
        username: string;
        success: string;
        tweet: string;
        hashtag: string;
        mention: string;
        subscribe: string;
        unsubscribe: string;
        results: string list;
        feed: string list list;
        error: string
    }