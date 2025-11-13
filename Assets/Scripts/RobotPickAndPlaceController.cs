using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RoboMechanicArm
{
    /// <summary>
    /// Controls the robot arm pick and place animation and movement logic.
    /// The controller expects an Animator with a trigger named <see cref="pickTriggerName"/>
    /// that plays a full pick-and-place cycle.
    /// </summary>
    public class RobotPickAndPlaceController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private Transform robotRoot;
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float rotationSpeed = 4f;
        [SerializeField] private Transform dropOffPoint;
        [SerializeField] private float pickupDistance = 0.4f;

        [Header("Animation")]
        [SerializeField] private Animator robotAnimator;
        [SerializeField] private string pickTriggerName = "Pick";
        [SerializeField, Tooltip("Duration of the pick animation in seconds.")]
        private float pickAnimationDuration = 4f;

        [Header("Gripper")]
        [SerializeField] private Transform gripAttachmentPoint;

        private readonly Queue<GameObject> _pendingCubes = new Queue<GameObject>();
        private readonly HashSet<GameObject> _queuedLookup = new HashSet<GameObject>();
        private GameObject _activeCube;
        private bool _isExecuting;

        private static readonly WaitForSeconds PollDelay = new WaitForSeconds(0.25f);

        private void Awake()
        {
            if (robotRoot == null)
            {
                robotRoot = transform;
            }
            if (robotAnimator == null)
            {
                robotAnimator = GetComponentInChildren<Animator>();
            }
        }

        /// <summary>
        /// Queue up the nearest cube tagged as "RedCube" for a pick-and-place cycle.
        /// </summary>
        public void QueuePickupOfNearestRedCube()
        {
            GameObject[] cubes = GameObject.FindGameObjectsWithTag("RedCube");
            if (cubes.Length == 0)
            {
                return;
            }

            GameObject closest = null;
            float closestDistance = float.MaxValue;
            Vector3 currentPosition = robotRoot.position;
            foreach (var cube in cubes)
            {
                if (cube == null || _queuedLookup.Contains(cube))
                {
                    continue;
                }

                float dist = Vector3.Distance(currentPosition, cube.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = cube;
                }
            }

            if (closest != null && _queuedLookup.Add(closest))
            {
                _pendingCubes.Enqueue(closest);
                if (!_isExecuting)
                {
                    StartCoroutine(ProcessQueue());
                }
            }
        }

        /// <summary>
        /// Clears any queued commands and stops tracking queued cubes.
        /// </summary>
        public void ClearQueuedActions()
        {
            _pendingCubes.Clear();
            _queuedLookup.Clear();
        }

        private IEnumerator ProcessQueue()
        {
            _isExecuting = true;
            while (_pendingCubes.Count > 0)
            {
                GameObject cube = _pendingCubes.Dequeue();
                _queuedLookup.Remove(cube);

                if (cube == null)
                {
                    continue;
                }

                _activeCube = cube;
                yield return MoveRobotTo(cube.transform.position);
                yield return ExecutePickAnimation();

                if (dropOffPoint != null)
                {
                    yield return MoveRobotTo(dropOffPoint.position);
                    yield return DropCube();
                }

                yield return PollDelay;
            }
            _isExecuting = false;
        }

        private IEnumerator MoveRobotTo(Vector3 targetPosition)
        {
            while (Vector3.Distance(new Vector3(robotRoot.position.x, 0f, robotRoot.position.z), new Vector3(targetPosition.x, 0f, targetPosition.z)) > pickupDistance)
            {
                Vector3 flatTarget = new Vector3(targetPosition.x, robotRoot.position.y, targetPosition.z);
                Vector3 direction = (flatTarget - robotRoot.position);
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    robotRoot.rotation = Quaternion.Slerp(robotRoot.rotation, lookRotation, rotationSpeed * Time.deltaTime);
                }

                robotRoot.position = Vector3.MoveTowards(robotRoot.position, flatTarget, moveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator ExecutePickAnimation()
        {
            if (robotAnimator == null)
            {
                if (_activeCube != null)
                {
                    AttachCubeToGripper(_activeCube);
                }
                yield break;
            }

            robotAnimator.ResetTrigger(pickTriggerName);
            robotAnimator.SetTrigger(pickTriggerName);

            float elapsed = 0f;
            while (elapsed < pickAnimationDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_activeCube != null)
            {
                AttachCubeToGripper(_activeCube);
            }
        }

        private IEnumerator DropCube()
        {
            if (_activeCube == null)
            {
                yield break;
            }

            DetachCube();
            yield return null;
        }

        private void AttachCubeToGripper(GameObject cube)
        {
            if (gripAttachmentPoint == null)
            {
                Debug.LogWarning("Grip attachment point not assigned.");
                return;
            }

            cube.transform.SetParent(gripAttachmentPoint);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localRotation = Quaternion.identity;
            Rigidbody rb = cube.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }

        private void DetachCube()
        {
            if (_activeCube == null)
            {
                return;
            }

            _activeCube.transform.SetParent(null);
            if (dropOffPoint != null)
            {
                _activeCube.transform.position = dropOffPoint.position;
            }
            Rigidbody rb = _activeCube.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
            }

            _activeCube.tag = "Untagged";
            _activeCube = null;
        }
    }
}
