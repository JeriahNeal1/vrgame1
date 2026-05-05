using System;
using System.Collections.Generic;
using HexabodyVR.PlayerController;
using HurricaneVR.Framework.Core.Grabbers;
using UnityEngine;

public class HexaTeleportDemo : MonoBehaviour
{
    public HexaBodyPlayer4 Hexa;
    public Transform TeleportPosition;
    public List<HVRHandGrabber> Hands = new List<HVRHandGrabber>();

    public bool Teleport;


    private void FixedUpdate()
    {
        if (Teleport)
        {
            Teleport = false;

            //MoveToPosition is based on the ball bottom, so we capture the ball bottom before teleporting
            var start = Hexa.LocoBall.transform.position + Vector3.down * Hexa.LocoCollider.radius;
            Hexa.MoveToPosition(TeleportPosition.position);
            var delta = TeleportPosition.position - start;

            for (int i = 0; i < Hands.Count; i++)
            {
                var hand = Hands[i];
                if (hand && hand.HeldObject && hand.HeldObject.Rigidbody)
                {
                    var rb = hand.HeldObject.Rigidbody;
                    rb.transform.position += delta;
                    rb.position += delta;
                }
            }
        }
    }
}