# lib-rcon
C# Rcon support for minecraft, including using rcon to modify minecraft servers without using mods.

This library will have support for a asynchronous TCP/IP client connecting to Minecraft's version of RCon allowing for a session
, authentication and command transmission/reception.  The RCon portion allows for synchronous operation as well.

Additionally, there is support for reading most NBT types to and from streams, and to load and traverse MCA region files. Will
be adding the ability to save altered MCA records as well.

A fill rendering system will also be in place that allows for "room" fabrication by sending fill commands to the rconsole and/or
sendkeys to a client with an operator player.
