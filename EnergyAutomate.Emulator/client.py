"""
Client for the grobro mqtt side, handling messages from/to
* growatt cloud
* growatt devices
"""

from http import client
import os
import signal
import time
import struct
import logging
import ssl
from typing import Callable

import paho.mqtt.client as mqtt
from paho.mqtt.client import MQTTMessage

class SignalHandler:
    """
    Catches SIGINT and SIGTERM in order to trigger
    graceful shutdown.
    """

    _caught: bool

    def __init__(self):
        self._running = True
        # Register signal handlers for graceful shutdown
        signal.signal(signal.SIGINT, self._handle)
        signal.signal(signal.SIGTERM, self._handle)

    def _handle(self, _, __):
        """
        Handles signal by setting RUNNING to false.
        """
        if self.log_callback:
            self.log_callback("Signal received, shutting down...")
        self._running = False

    @property
    def caught(self) -> bool:
        """
        Wether the signal was caught.
        """
        return self._running

class Client:

    _brokerHost = "localhost"
    _brokerPort = 7006
    _growattHost = "mqtt.growatt.com"
    _growattPort = 7006
    _clientId = ""

    _clientBroker: mqtt.Client
    _clientGrowatt: mqtt.Client
   
    def set_log_callback(self, log_callback=None):
        self.log_callback = log_callback
        
        if self.log_callback:
            self.log_callback("Logging initialized!")

    def set_dump_callback(self, dump_callback=None):
        self.dump_callback = dump_callback

    def set_options(self, options):
        if self.log_callback:
            self.log_callback(f"[TRACE] Python received options: ClientId={options.ClientId}, BrokerHost={options.BrokerHost}, BrokerPort={options.BrokerPort}, GrowattHost={options.GrowattHost}, GrowattPort={options.GrowattPort}")
        self._clientId = options.ClientId
        self._brokerHost = options.BrokerHost
        self._brokerPort = options.BrokerPort
        self._growattHost = options.GrowattHost
        self._growattPort = options.GrowattPort

    def set_clientid(self, clientId):
        if self.log_callback:
            self.log_callback(f"[TRACE] Python received clientid: {clientId}")
        self._clientId = clientId

    def start(self):
        if self.log_callback:
            self.log_callback("Python client started!")

        self.__connect_to_growatt_server()
        self.__connect_to_broker()

        signal_handler = SignalHandler()

        try:
            while signal_handler.caught:
                time.sleep(0.1)
        finally:
            if self.log_callback:
                self.log_callback("Stopped both clients. Exiting...")

    def stop(self):
        if self.log_callback:
            self.log_callback("GroBro: Stop")
        self._clientBroker.loop_stop()
        self._clientBroker.disconnect()
        self._clientGrowatt.loop_stop()
        self._clientGrowatt.disconnect()

        if self.log_callback:
            self.log_callback("Python client finished!")

    def __on_broker_disconnect(self, client: mqtt.Client, userdata, rc):
        if self.log_callback:
            self.log_callback("Disconnected from Broker, trying to reconnect...")
            self.__connect_to_broker(client)

    def __connect_to_broker(self):               
        if self.log_callback:
            self.log_callback("[TRACE] Attempting to connect to ah.azure.sidata.com:7006 ...")

        self._clientBroker = mqtt.Client(client_id="relay")
        self._clientBroker.tls_set(cert_reqs=ssl.CERT_NONE)
        self._clientBroker.tls_insecure_set(True)
        self._clientBroker.on_disconnect = self.__on_broker_disconnect
        self._clientBroker.on_message = self.__on_message

        while not self._clientBroker.is_connected():
            try:
                self._clientBroker.connect(self._brokerHost, self._brokerPort, 60)
                self._clientBroker.loop_start()
                if self.log_callback:
                    self.log_callback("Subscribe c/#")

                # Nach dem Start kurz warten, damit der Status aktualisiert wird
                time.sleep(1)
                if self._clientBroker.is_connected():
                    if self.log_callback:
                        self.log_callback("[TRACE] Successfully connected to Broker.")

                    self._clientBroker.subscribe("c/#")
                    break
            except Exception as e:
                if self.log_callback:
                    self.log_callback(f"[TRACE] Connection attempt failed: {e}. Waiting 5 seconds before next reconnect attempt...")
                time.sleep(5)

    def __on_growatt_disconnect(self, client, userdata, rc):
        if self.log_callback:
            self.log_callback("Disconnected from Growatt, trying to reconnect...")
            self.__connect_to_growatt_server()

    def __connect_to_growatt_server(self):

        if self.log_callback:
            self.log_callback(f"Connecting to Growatt broker at 'mqtt.growatt.com:7006' with clientID: {self._clientId}")

        self._clientGrowatt = mqtt.Client(client_id=self._clientId)
        self._clientGrowatt.tls_set(cert_reqs=ssl.CERT_NONE)
        self._clientGrowatt.tls_insecure_set(True)
        self._clientGrowatt.on_message = self.__on_message_forward_client
        self._clientGrowatt.on_disconnect = self.__on_growatt_disconnect

        while not self._clientGrowatt.is_connected():
            try:
                if self.log_callback:
                    self.log_callback("[TRACE] Attempting to connect to mqtt.growatt.com:7006 ...")

                self._clientGrowatt.connect(self._growattHost,  self._growattPort, 60)
                self._clientGrowatt.loop_start()

                time.sleep(1)
                if self._clientGrowatt.is_connected():
                    if self.log_callback:
                        self.log_callback("[TRACE] Successfully connected to Growatt.")  
                    break
            except Exception as e:
                if self.log_callback:
                    self.log_callback(f"[TRACE] Connection attempt failed: {e}. Waiting 30 seconds before next reconnect attempt...")
                time.sleep(30)

    def __on_message(self, client, userdata, msg: MQTTMessage):
        try:
            if self.dump_callback:
                self.dump_callback(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)

            device_id = msg.topic.split("/")[-1]
            
            if self.log_callback:
                self.log_callback(f"Message from Broker, Topic: {msg.topic}, DeviceID: {device_id}")

            self._clientGrowatt.publish(
                msg.topic,
                payload=msg.payload,
                qos=msg.qos,
                retain=msg.retain,
            )
        except Exception as e:

            self.log_callback(f"Error Processing message: {e}")

    def __on_message_forward_client(self, client, userdata, msg: MQTTMessage):
        try:
            if self.dump_callback:
                self.dump_callback(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)

            device_id = msg.topic.split("/")[-1]

            if self.log_callback:
                self.log_callback(f"Message from Growatt, Topic: {msg.topic}, DeviceID: {device_id}")

            # We need to publish the messages from Growatt on the Topic
            # s/33/{deviceid}. Growatt sends them on Topic s/{deviceid}
            self._clientBroker.publish(
                msg.topic.split("/")[0] + "/33/" + device_id,
                payload=msg.payload,
                qos=msg.qos,
                retain=msg.retain,
            )
        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Forwarding message: {e}")



