# stomrin-worker

Watches for .plz files requesting the calendar for an addres, and generates
.html/.ics files or .err files as appropriate by calling into Omrin their
backend.

## How Omrin used to work around 2011-ish

They used to offer a Silverlight applet that called a WCF service on a
[non-standard port][1]. Surely you can think of multiple reasons why all that
sucked based on the previous sentence alone.

As of 2020Q4, the WCF thing still seems to be alive and kicking. Of course it's
anyone's guess whether this WCF service is still there because their weird PHP
applications depend on it (more on that later), or if everybody has just
forgotten about it. It's 50-50 really.

[1]: http://pb-portals.omrin.nl:7980/burgerportal/ServiceKalender.svc

## How Omrin does it as of 2020Q4

Fast-forward nearly ten years! Now it sucks for new and additional reasons! It
starts simple enough: the user navigates to a web page, fills out an HTML form
with their address info, and POSTs it.

Here, the programmer snorted a big line of cocaine and then decided to proceed
as follows: use the [serialize()][2] function from PHP to return the form data
from the request in a Set-Cookie header for the `address` cookie, then 302
redirect to another page that reads the `address` cookie, calls PHP
`unserialize()` on it, generates a blob of JSON and inject a script block right
into the page that sets `omrinDataYears` and `omrinDataGroups` global
variables, and then use jQuery to put together an HTML table.

[2]: https://www.php.net/manual/en/function.serialize.php

Of course, it's interesting to think about the security implications of calling
`unserialize()` on untrusted data. What is that you say, the docs for
[unserialize()][3] specifically warn against calling this on untrusted data?
You can find [ready-made exploits][4] for unsafe uses of `unserialize()`?
[#YOLO][5]

[3]: https://www.php.net/manual/en/function.unserialize.php
[4]: https://nitesculucian.github.io/2018/10/05/php-object-injection-cheat-sheet/

Anyway, I think the WCF thing is going to be more stable from code change than
this PHP/JS frontend monstrosity. The WCF thing has been unchanged for 10 years
now.
